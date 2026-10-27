using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace LapWest {
    public sealed class LampEstimate {
        public string ColorName="?",Decision="no usable colored pixels";
        public int RedPixels,GreenPixels,YellowPixels;
        public int VisibilityPixels,ObstructionPixels;
        public double RedWeight,GreenWeight,YellowWeight;
        public RectangleF Region;
        public string Evidence() {
            return String.Format(CultureInfo.InvariantCulture,
                "{0}; decision={1}; R/G/Y pixels={2}/{3}/{4}; weights={5:F2}/{6:F2}/{7:F2}; region={8}; obstruction_pixels={9}/{10}",
                ColorName,Decision,RedPixels,GreenPixels,YellowPixels,RedWeight,GreenWeight,YellowWeight,Region,ObstructionPixels,VisibilityPixels);
        }
    }
    public sealed class SignalColorReading {
        public LampEstimate M2=new LampEstimate();
        public LampEstimate M1=new LampEstimate();
        public bool Located;
        public BoxAlignment Alignment;
        public double SceneScore=-1,Scale=1;
        public RectangleF WatchRegion;
        public string Decision="waiting for image";
    }
    // Display-only estimates from the user-labeled reflected-light positions.
    // These are not verified physical signal-head detections or indications.
    // A separate background fit keeps track positions independent of whichever
    // red reflection the ON/OFF detector currently follows.
    public sealed class SignalColorDisplay {
        public const double AlignmentMinimum=.74;
        readonly SceneLocator locator=new SceneLocator(true);
        readonly Settings cfg;
        readonly RectangleF m2Area,m1Area,colorBand;
        readonly float contextX,contextY;
        // Coordinates in the 630 x 318 camera preview supplied 2026-10-25.
        // M2 is the left green/cyan or yellow cue. M1 is the separate right
        // red cue. Head order is intentionally not inferred.
        static readonly RectangleF M2Area=Area(371,131,18,11);
        // User-confirmed M1 is immediately beside M2, at the same height.
        static readonly RectangleF M1Area=Area(390,131,18,11);
        static RectangleF Area(float x,float y,float w,float h) {
            return new RectangleF(x/630f,y/318f,w/630f,h/318f);
        }
        public SignalColorDisplay(string root) : this(root,new Settings()) {}
        public SignalColorDisplay(string root,Settings settings) {
            cfg=settings;
            using(Bitmap source=new Bitmap(Path.Combine(root,"samples/signal-colors-m2-green.png")))
            using(FrameContent reference=new FrameContent(source)) {
                m2Area=reference.ToContent(M2Area);m1Area=reference.ToContent(M1Area);
                colorBand=reference.ToContent(Area(350,126,109,23));
                contextX=18f/630/reference.Bounds.Width;contextY=12f/318/reference.Bounds.Height;
                locator.SetReference(reference.Image,reference.ToContent(Area(350,116,109,43)));
            }
        }
        public BoxAlignment Locate(Bitmap source) {
            using(FrameContent content=new FrameContent(source))return new BoxAlignment(locator.LocateColor(SceneLocator.Thumbnail(content.Image),AlignmentMinimum),content.Bounds);
        }
        RectangleF ReferenceSample(bool m1) {DetectionBox manual=m1?cfg.M1Box:cfg.M2Box;return manual.Enabled?manual.Area:(m1?m1Area:m2Area);}
        public RectangleF SampleArea(bool m1,BoxAlignment alignment) {return alignment.ToFrame(ReferenceSample(m1));}
        public SignalColorReading Analyze(Bitmap source) {return Analyze(source,null);}
        public SignalColorReading Analyze(Bitmap source,BoxAlignment alignment) {
            using(FrameContent content=new FrameContent(source)) {
                if(alignment==null)alignment=new BoxAlignment(locator.LocateColor(SceneLocator.Thumbnail(content.Image),AlignmentMinimum),content.Bounds);
                SignalColorReading result=AnalyzeContent(content.Image,alignment);
                // Keep geometry available for the debug overlay even when the
                // background fit is rejected. Unconfirmed boxes are never sampled.
                result.M2.Region=content.ToFrame(result.M2.Region);result.M1.Region=content.ToFrame(result.M1.Region);
                if(!result.WatchRegion.IsEmpty)result.WatchRegion=content.ToFrame(result.WatchRegion);
                return result;
            }
        }
        SignalColorReading AnalyzeContent(Bitmap frame,BoxAlignment alignment) {
            SignalColorReading result=new SignalColorReading();SceneLocation fit=alignment.Fit;
            result.Alignment=alignment;
            result.Located=fit.Found;result.SceneScore=fit.Score;result.Scale=fit.Scale;
            if(!fit.Found) {
                result.M2.Region=SceneLocator.Project(ReferenceSample(false),fit);
                result.M1.Region=SceneLocator.Project(ReferenceSample(true),fit);
                result.M2.Decision=result.M1.Decision="not sampled: color-reference alignment unconfirmed";
                locator.Reset();result.Decision="color reference view not confirmed; dashed boxes show candidate positions, not active color samples";return result;
            }
            RectangleF m2=ReferenceSample(false),m1=ReferenceSample(true);
            result.M2=SampleVisible(frame,m2,fit,cfg.M2Box.Enabled);
            result.M1=SampleVisible(frame,m1,fit,cfg.M1Box.Enabled);
            RectangleF m2Context=m2,m1Context=m1;
            m2Context.Inflate(contextX,contextY);m1Context.Inflate(contextX,contextY);
            result.WatchRegion=RectangleF.Union(SceneLocator.Project(m2Context,fit),SceneLocator.Project(m1Context,fit));
            result.Decision="reflected-color estimates; M2 left / M1 right; head order not inferred";
            return result;
        }
        LampEstimate SampleVisible(Bitmap frame,RectangleF referenceArea,SceneLocation fit,bool manual) {
            RectangleF area=SceneLocator.Project(referenceArea,fit);
            LampEstimate result=Sample(frame,area);
            RectangleF context=referenceArea;context.Inflate(contextX,contextY);
            context=SceneLocator.Project(context,fit);
            if(!new RectangleF(0,0,1,1).Contains(context)) {
                result.ColorName="?";result.Decision="visibility neighborhood outside picture";return result;
            }
            // Keep real signal-color changes out of the obstruction check.
            // Compare the surrounding expected-open scene with its registered
            // reference. This does not rely on the train classifier succeeding.
            RectangleF excludedColorBand=SceneLocator.Project(colorBand,fit);
            RectangleF movedColorBand=referenceArea;
            movedColorBand.Inflate(3f/630,3f/318);movedColorBand=SceneLocator.Project(movedColorBand,fit);
            int left=(int)(context.Left*frame.Width),right=(int)(context.Right*frame.Width);
            int top=(int)(context.Top*frame.Height),bottom=(int)(context.Bottom*frame.Height);
            for(int y=top;y<bottom;y++)for(int x=left;x<right;x++) {
                if(excludedColorBand.Contains((float)x/frame.Width,(float)y/frame.Height)||(manual&&movedColorBand.Contains((float)x/frame.Width,(float)y/frame.Height)))continue;
                Color c=frame.GetPixel(x,y);
                if(c.R>170&&c.G>170&&c.B<100)continue;
                double baseline;
                if(!locator.ReferenceWhitePeak(fit,x*SceneLocator.Width/(double)frame.Width,y*SceneLocator.Height/(double)frame.Height,out baseline))continue;
                result.VisibilityPixels++;
                double level=.299*c.R+.587*c.G+.114*c.B;
                if(level>45&&level>baseline+25)result.ObstructionPixels++;
            }
            int colored=result.RedPixels+result.GreenPixels+result.YellowPixels;
            double sampleArea=area.Width*frame.Width*area.Height*frame.Height;
            if(result.VisibilityPixels<30||result.ObstructionPixels>=Math.Max(8,result.VisibilityPixels*.08)||colored>sampleArea*.45) {
                result.ColorName="?";result.Decision="signal visibility unconfirmed / foreground obstruction";
            }
            return result;
        }
        // No resizing before color sampling: the small colored fringes matter.
        // Cyan is accepted as green evidence in this night-camera calibration;
        // pure blue, white glare and yellow annotation borders are excluded.
        public static LampEstimate Sample(Bitmap image,RectangleF area) {
            LampEstimate result=new LampEstimate();result.Region=area;
            if(!new RectangleF(0,0,1,1).Contains(area)) {result.Decision="sample outside picture";return result;}
            Rectangle r=Rectangle.FromLTRB((int)Math.Floor(area.Left*image.Width),(int)Math.Floor(area.Top*image.Height),
                (int)Math.Ceiling(area.Right*image.Width),(int)Math.Ceiling(area.Bottom*image.Height));
            r=Rectangle.Intersect(r,new Rectangle(0,0,image.Width,image.Height));
            if(r.Width<3||r.Height<2) {result.Decision="sample too small";return result;}
            int glare=0,annotation=0;
            for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++) {
                Color c=image.GetPixel(x,y);double high=Math.Max(c.R,Math.Max(c.G,c.B)),low=Math.Min(c.R,Math.Min(c.G,c.B));
                if(c.R>170&&c.G>170&&c.B<100) {annotation++;continue;}
                if(low>150&&high-low<55) {glare++;continue;}
                if(high<30||high-low<15)continue;
                double weight=(high-low)/255.0;
                if(c.G>=30&&c.G>c.R*1.15+7&&c.B<=c.G*1.8&&c.G>c.B*.52) {
                    result.GreenPixels++;result.GreenWeight+=weight;
                }else if(c.R>=40&&c.G>=25&&c.R>=c.G*.95&&c.R<c.G*2.7&&c.G>c.B*1.22+5) {
                    // Night-camera yellow often appears orange/amber. Check
                    // its green-to-blue separation before the broader red rule.
                    result.YellowPixels++;result.YellowWeight+=weight;
                }else if(c.R>=30&&c.R>c.G*1.25+7&&c.R>c.B*1.06+3) {
                    result.RedPixels++;result.RedWeight+=weight;
                }
            }
            if(glare>r.Width*r.Height*.20||annotation>r.Width*r.Height*.20) {
                result.Decision="glare or annotation obscures sample";return result;
            }
            double[] values={result.RedWeight,result.GreenWeight,result.YellowWeight};
            int[] counts={result.RedPixels,result.GreenPixels,result.YellowPixels};
            string[] names={"RED","GREEN","YELLOW"};
            int best=0;for(int i=1;i<3;i++)if(values[i]>values[best])best=i;
            double other=0;for(int i=0;i<3;i++)if(i!=best)other=Math.Max(other,values[i]);
            double areaScale=image.Width/630.0*image.Height/318.0;
            int minimum=Math.Max(3,(int)Math.Round(3*areaScale));
            double weightMinimum=.28*Math.Max(.6,areaScale);
            if(counts[best]<minimum||values[best]<weightMinimum) {result.Decision="too little color evidence";return result;}
            if(other>values[best]*.50) {result.Decision="mixed colors";return result;}
            result.ColorName=names[best];
            result.Decision="dominant color in calibrated track zone; head order unknown";
            return result;
        }
    }
}
