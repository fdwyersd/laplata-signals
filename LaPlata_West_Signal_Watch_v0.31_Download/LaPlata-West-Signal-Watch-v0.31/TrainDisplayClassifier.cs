using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace LapWest {
    // Display-only recognition over the wider picture. No signal ROI, signal
    // lock, red reflection or signal-color gate is used to identify M1/M2.
    public sealed class TrainDisplayClassifier {
        internal sealed class Features {
            public double[] Values;
            public int Pixels,SpanX,SpanY;
        }
        sealed class Example {
            public string Label,Name;
            public Features Features;
        }
        sealed class Background {
            public SceneLocator Locator=new SceneLocator(true);
            public double[] Pixels;
            public List<Example> Examples=new List<Example>();
        }
        readonly List<Background> backgrounds=new List<Background>();
        readonly M2AdditionalExample additional;
        readonly TrainViewExamples currentView;
        public string CurrentViewStatus {get{return currentView.Status;}}
        public string AdditionalExampleStatus {get{return additional.Status;}}
        const int W=SceneLocator.Width,H=SceneLocator.Height;
        public TrainDisplayClassifier(Settings settings,string root) {
            string[] empty={"off.png","curve-view.jpg"};
            string[] names={"train-m1.png","train-m1-tanks.png","train-m2.png","train-m2-dim.png","train-m2-cars.jpg","train-m2-reflections.png","train-m1-reflections.png"};
            string[] labels={"M1 TRAIN","M1 TRAIN","M2 TRAIN","M2 TRAIN","M2 TRAIN","M2 TRAIN","M1 TRAIN"};
            foreach(string file in empty) {
                Background bg=new Background();
                using(Bitmap image=new Bitmap(Path.Combine(root,"samples/"+file))) {
                    bg.Pixels=Thumbnail(image);
                    bg.Locator.SetReference(image,new RectangleF(.585f,.19f,.18f,.10f));
                }
                for(int i=0;i<names.Length;i++)using(Bitmap image=new Bitmap(Path.Combine(root,"samples/"+names[i]))) {
                    double[] pixels=Thumbnail(image);bg.Locator.Reset();SceneLocation fit=bg.Locator.Locate(pixels,.78);
                    if(!ValidFit(fit))continue;
                    Example e=new Example();e.Label=labels[i];e.Name=names[i];e.Features=Foreground(pixels,bg.Pixels,fit);bg.Examples.Add(e);
                }
                bg.Locator.Reset();if(bg.Examples.Count>0)backgrounds.Add(bg);
            }
            if(backgrounds.Count==0)throw new Exception("No train calibration could be aligned to the background.");
            additional=new M2AdditionalExample(root);
            currentView=new TrainViewExamples(root);
        }
        internal static bool ValidFit(SceneLocation fit) {return fit.Found&&!fit.Ambiguous&&fit.Score>=.80&&fit.Landmarks>=2&&fit.PatchMinimum>=.65;}
        internal static double[] Thumbnail(Bitmap image) {
            int w=Math.Min(1280,image.Width),h=(int)Math.Round((double)image.Height*w/image.Width);
            using(Bitmap clean=Detector.Scale(image,w,h,false)) {
                BitmapData data=clean.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format24bppRgb);
                try {
                    int stride=Math.Abs(data.Stride);byte[] bytes=new byte[stride*h];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
                    // Ignore pure-yellow dashboard annotations in supplied screen
                    // captures. Actual camera images have no dashboard rectangle.
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                        int k=y*stride+x*3;
                        if(bytes[k+2]>220&&bytes[k+1]>220&&bytes[k]<45&&Math.Abs(bytes[k+2]-bytes[k+1])<8)bytes[k]=bytes[k+1]=bytes[k+2]=0;
                    }
                    Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
                }finally{clean.UnlockBits(data);}
                return SceneLocator.Thumbnail(clean);
            }
        }
        static double Pixel(double[] pixels,double x,double y) {
            if(x<0||y<0||x>=W-1||y>=H-1)return 0;
            int xx=(int)x,yy=(int)y;double fx=x-xx,fy=y-yy;
            return (pixels[yy*W+xx]*(1-fx)+pixels[yy*W+xx+1]*fx)*(1-fy)+(pixels[(yy+1)*W+xx]*(1-fx)+pixels[(yy+1)*W+xx+1]*fx)*fy;
        }
        internal static Features Foreground(double[] pixels,double[] empty,SceneLocation fit) {
            Features f=new Features();double[] raw=new double[W*H];int left=W,right=0,top=H,bottom=0;
            // Use the full picture in background coordinates. Only the printed
            // header and bottom watermark band are omitted, never the signal box.
            for(int y=8;y<H-7;y++)for(int x=2;x<W-2;x++) {
                double tx=x*fit.Scale+fit.Dx,ty=y*fit.Scale+fit.Dy;
                if(tx<1||ty<1||tx>=W-2||ty>=H-2)continue;
                double a=Pixel(pixels,tx,ty),b=empty[y*W+x];
                // Suppress the permanent bright fixtures and their small
                // alignment differences. Cars occupy the darker track scene.
                double value=a>=22?Math.Max(0,Math.Min(1,(a-b-12)/35))*(1-Math.Min(.9,b/60)):0;
                raw[y*W+x]=value;
                if(value>.22){f.Pixels++;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            }
            f.SpanX=f.Pixels>0?right-left+1:0;f.SpanY=f.Pixels>0?bottom-top+1:0;f.Values=new double[raw.Length];
            // Smooth over panel spacing and small alignment differences, so the
            // spatial occupation of the tracks matters more than exact panels.
            for(int y=8;y<H-7;y++)for(int x=2;x<W-2;x++) {
                double sum=0;int n=0;
                for(int yy=Math.Max(8,y-1);yy<=Math.Min(H-8,y+1);yy++)for(int xx=Math.Max(2,x-2);xx<=Math.Min(W-3,x+2);xx++){sum+=raw[yy*W+xx];n++;}
                f.Values[y*W+x]=sum/Math.Max(1,n);
            }
            return f;
        }
        internal static double Similarity(Features a,Features b) {
            double aa=0,bb=0,ab=0;
            for(int i=0;i<a.Values.Length;i++){aa+=a.Values[i]*a.Values[i];bb+=b.Values[i]*b.Values[i];ab+=a.Values[i]*b.Values[i];}
            return aa>1&&bb>1?ab/Math.Sqrt(aa*bb):0;
        }
        public void Apply(Bitmap image,Reading reading) {
            ApplyBaseline(image,reading);
            if(reading==null||reading.TrainDisplayLabel.Length>0||
                (reading.State!="CHECK IMAGE"&&reading.State!="SIGNALS ON"&&reading.State!="SIGNALS OFF"))return;
            additional.Apply(image,reading);
            if(reading.TrainDisplayLabel.Length==0)currentView.Apply(image,reading);
        }
        void ApplyBaseline(Bitmap image,Reading reading) {
            if(reading==null||(reading.State!="CHECK IMAGE"&&reading.State!="SIGNALS ON"&&reading.State!="SIGNALS OFF"))return;
            string west=reading.TrainDisplayLabel=="WEST CURVE TRAIN"?reading.TrainDisplayLabel:"";
            reading.TrainDisplayLabel=west;reading.TrainDecision="background_not_confirmed";
            double[] pixels=Thumbnail(image);double m1=0,m2=0,m1Scene=-1,m2Scene=-1;int m1Body=0,m2Body=0;
            string name1="",name2="";bool bodyCandidate=false;
            foreach(Background bg in backgrounds) {
                SceneLocation fit=bg.Locator.Locate(pixels,.78);
                if(!ValidFit(fit))continue;
                reading.TrainSceneFound=true;reading.TrainSceneScore=Math.Max(reading.TrainSceneScore,fit.Score);
                Features f=Foreground(pixels,bg.Pixels,fit);
                reading.TrainBodyPixels=Math.Max(reading.TrainBodyPixels,f.Pixels);
                reading.TrainSpanX=Math.Max(reading.TrainSpanX,f.SpanX);reading.TrainSpanY=Math.Max(reading.TrainSpanY,f.SpanY);
                if(f.Pixels<180||f.SpanX<50||f.SpanY<12){reading.TrainDecision="insufficient_car_extent";continue;}
                bodyCandidate=true;
                foreach(Example e in bg.Examples) {
                    double score=Similarity(f,e.Features);
                    if(e.Label=="M1 TRAIN"&&score>m1){m1=score;name1=e.Name;m1Scene=fit.Score;m1Body=f.Pixels;}
                    if(e.Label=="M2 TRAIN"&&score>m2){m2=score;name2=e.Name;m2Scene=fit.Score;m2Body=f.Pixels;}
                }
            }
            reading.TrainM1Score=m1;reading.TrainM2Score=m2;
            double best=Math.Max(m1,m2),margin=Math.Abs(m1-m2);
            reading.TrainExample=m1>m2?name1:name2;
            reading.TrainMatchSceneScore=m1>m2?m1Scene:m2Scene;
            reading.TrainMatchBodyPixels=m1>m2?m1Body:m2Body;
            if(best>=.78) {
                if(margin<.06){reading.TrainDecision="track_ambiguous";return;}
                reading.TrainAcceptanceMode="strong_pattern";
            }else {
                // The original source-frame miss scored 0.73014 vs 0.43469.
                // Permit a weaker appearance match only with much stronger
                // track separation, background agreement and car extent, all
                // from the SAME background fit as the winning appearance score.
                if(best<.70||reading.TrainMatchSceneScore<.90||reading.TrainMatchBodyPixels<300) {
                    if(bodyCandidate)reading.TrainDecision="car_pattern_not_confirmed";return;
                }
                if(margin<.20){reading.TrainDecision="track_ambiguous";return;}
                reading.TrainAcceptanceMode="separated_pattern";
            }
            reading.TrainDecision="whole_picture_car_pattern";reading.TrainDisplayLabel=m1>m2?"M1 TRAIN":"M2 TRAIN";
        }
    }
}
