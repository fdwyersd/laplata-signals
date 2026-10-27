using System;
using System.Drawing;
using System.Collections.Generic;

namespace LapWest {
    // Fit only the known west-facing background. Signal pixels and the printed
    // timestamp are excluded from the background samples. No learned model,
    // native computer-vision package, or camera-control connection is required.
    public sealed class SceneLocation {
        public bool Found,Ambiguous;
        public double Score=-1,Scale=1,Dx,Dy,PatchMinimum;
        public int Reference=-1,Landmarks;
        public string Method="searching";
    }
    sealed class SceneLocator {
        internal const int Width=160,Height=90;
        sealed class Reference {
            public double[] Pixels,Soft;
            public Point[] Samples,Landmarks;
            public RectangleF Area;
            public byte[] Brightness;
            public int NativeWidth,NativeHeight;
        }
        readonly bool staticTrainBackground;
        public SceneLocator() {}
        public SceneLocator(bool excludeTrainCorridors) {staticTrainBackground=excludeTrainCorridors;}
        readonly List<Reference> references=new List<Reference>();
        SceneLocation last;
        public static double[] Thumbnail(Bitmap source) {
            double[] a=new double[Width*Height];
            using(Bitmap small=Detector.Scale(source,Width,Height,false))
                for(int y=0;y<Height;y++)for(int x=0;x<Width;x++) {
                    Color c=small.GetPixel(x,y);a[y*Width+x]=.299*c.R+.587*c.G+.114*c.B;
                }
            return a;
        }
        static double[] Blur(double[] a) {
            double[] b=new double[a.Length];
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++) {
                double sum=0;int count=0;
                for(int yy=Math.Max(0,y-1);yy<=Math.Min(Height-1,y+1);yy++)for(int xx=Math.Max(0,x-1);xx<=Math.Min(Width-1,x+1);xx++){sum+=a[yy*Width+xx];count++;}
                b[y*Width+x]=sum/count;
            }
            return b;
        }
        static Point Peak(double[] a,int left,int top,int right,int bottom) {
            Point best=new Point(left,top);double level=-1;
            for(int y=top;y<bottom;y++)for(int x=left;x<right;x++)if(a[y*Width+x]>level){level=a[y*Width+x];best=new Point(x,y);}
            return best;
        }
        Reference MakeReference(Bitmap image,RectangleF area) {
            Reference r=new Reference();r.Area=area;r.Pixels=Thumbnail(image);r.Soft=Blur(r.Pixels);
            r.NativeWidth=Math.Min(1280,image.Width);r.NativeHeight=(int)Math.Round((double)image.Height*r.NativeWidth/image.Width);
            r.Brightness=new byte[r.NativeWidth*r.NativeHeight];
            using(Bitmap native=Detector.Scale(image,r.NativeWidth,r.NativeHeight,false))for(int y=0;y<r.NativeHeight;y++)for(int x=0;x<r.NativeWidth;x++) {
                Color c=native.GetPixel(x,y);
                // Supplied reference rectangles are annotations, not lights.
                r.Brightness[y*r.NativeWidth+x]=c.R>170&&c.G>170&&c.B<100?(byte)0:(byte)(.299*c.R+.587*c.G+.114*c.B);
            }
            List<Point> points=new List<Point>();
            for(int y=8;y<53;y+=2)for(int x=3;x<158;x+=3) {
                // Display-only scene alignment must exclude passing cars too.
                if(staticTrainBackground&&x>=12&&x<=132&&y>=20)continue;
                // The rail-reflection neighborhood can change ON/OFF and must
                // not be required to locate a scene with the signals dark.
                if(x>=(area.Left-.16)*Width-3&&x<=(area.Right+.12)*Width+3&&y>=(area.Top-.08)*Height-2&&y<=(area.Bottom+.08)*Height+2)continue;
                double level=r.Pixels[y*Width+x];
                if(level>8&&(level>35||Math.Abs(level-r.Pixels[y*Width+x-2])>3||Math.Abs(level-r.Pixels[(y+2)*Width+x])>3))points.Add(new Point(x,y));
            }
            if(points.Count<35)throw new Exception("OFF reference has too little visible scene detail.");
            if(points.Count>220) {
                List<Point> reduced=new List<Point>();
                for(int i=0;i<220;i++)reduced.Add(points[(int)((long)i*points.Count/220)]);
                points=reduced;
            }
            r.Samples=points.ToArray();
            r.Landmarks=staticTrainBackground?
                new Point[]{Peak(r.Soft,0,6,12,33),Peak(r.Soft,133,22,159,56)}:
                new Point[]{Peak(r.Soft,0,6,45,33),Peak(r.Soft,110,22,159,56),Peak(r.Soft,42,26,94,53)};
            return r;
        }
        public int Count {get{return references.Count;}}
        public RectangleF ReferenceArea(int index) {return references[index].Area;}
        public bool ReferenceValue(SceneLocation fit,double targetX,double targetY,out double value) {
            double x=(targetX-fit.Dx)/fit.Scale,y=(targetY-fit.Dy)/fit.Scale;value=0;
            if(x<0||y<0||x>=Width-1||y>=Height-1)return false;
            value=Interpolate(references[fit.Reference].Pixels,x,y);return true;
        }
        public bool ReferenceWhitePeak(SceneLocation fit,double targetX,double targetY,out double value) {
            Reference r=references[fit.Reference];value=0;
            double x=(targetX-fit.Dx)/fit.Scale/Width*r.NativeWidth,y=(targetY-fit.Dy)/fit.Scale/Height*r.NativeHeight;
            if(x<0||y<0||x>=r.NativeWidth||y>=r.NativeHeight)return false;
            int cx=(int)Math.Round(x),cy=(int)Math.Round(y),radius=Math.Max(2,(int)Math.Ceiling((double)r.NativeWidth/Width*1.25));
            for(int yy=Math.Max(0,cy-radius);yy<=Math.Min(r.NativeHeight-1,cy+radius);yy++)for(int xx=Math.Max(0,cx-radius);xx<=Math.Min(r.NativeWidth-1,cx+radius);xx++)value=Math.Max(value,r.Brightness[yy*r.NativeWidth+xx]);
            return true;
        }
        public void SetReference(Bitmap image,RectangleF area) {
            Reference candidate=MakeReference(image,area);references.Clear();references.Add(candidate);last=null;
        }
        public void AddReference(Bitmap image,RectangleF area) {references.Add(MakeReference(image,area));}
        public void Reset() {last=null;}
        static double Score(Reference r,double[] target,double scale,double dx,double dy) {
            double sa=0,sb=0,saa=0,sbb=0,sab=0;int n=0;
            foreach(Point p in r.Samples) {
                int x=(int)Math.Round(p.X*scale+dx),y=(int)Math.Round(p.Y*scale+dy);
                if(x<1||x>=Width-1||y<1||y>=Height-1)continue;
                double a=r.Soft[p.Y*Width+p.X],b=target[y*Width+x];n++;
                sa+=a;sb+=b;saa+=a*a;sbb+=b*b;sab+=a*b;
            }
            if(n<Math.Max(30,(int)(r.Samples.Length*.65)))return -1;
            double va=n*saa-sa*sa,vb=n*sbb-sb*sb;
            if(va<n*n*9||vb<n*n*9)return -1;
            return (n*sab-sa*sb)/Math.Sqrt(va*vb);
        }
        static double Interpolate(double[] a,double x,double y) {
            int ix=(int)x,iy=(int)y;double fx=x-ix,fy=y-iy;
            return (a[iy*Width+ix]*(1-fx)+a[iy*Width+ix+1]*fx)*(1-fy)+(a[(iy+1)*Width+ix]*(1-fx)+a[(iy+1)*Width+ix+1]*fx)*fy;
        }
        static bool LandmarksMatch(Reference r,double[] target,SceneLocation fit) {
            List<Point> matched=new List<Point>();double minimum=1;
            foreach(Point center in r.Landmarks) {
                double sa=0,sb=0,saa=0,sbb=0,sab=0,peak=0;int n=0;
                for(int yy=-6;yy<=6;yy+=2)for(int xx=-8;xx<=8;xx+=2) {
                    int rx=center.X+xx,ry=center.Y+yy;
                    double tx=rx*fit.Scale+fit.Dx,ty=ry*fit.Scale+fit.Dy;
                    if(rx<0||rx>=Width||ry<0||ry>=Height||tx<0||ty<0||tx>=Width-1||ty>=Height-1)continue;
                    double a=r.Soft[ry*Width+rx],b=Interpolate(target,tx,ty);n++;peak=Math.Max(peak,b);
                    sa+=a;sb+=b;saa+=a*a;sbb+=b*b;sab+=a*b;
                }
                if(n<30||peak<20)continue;
                double den=Math.Sqrt(Math.Max(0,(n*saa-sa*sa)*(n*sbb-sb*sb)));
                double correlation=den>n*n*4?(n*sab-sa*sb)/den:-1;
                if(correlation>=.62){matched.Add(center);minimum=Math.Min(minimum,correlation);}
            }
            fit.Landmarks=matched.Count;fit.PatchMinimum=matched.Count>0?minimum:0;
            if(matched.Count<2)return false;
            // Two separated, structured patches must agree on the same fit.
            for(int i=0;i<matched.Count;i++)for(int j=i+1;j<matched.Count;j++)
                if(Math.Abs(matched[i].X-matched[j].X)>=25)return true;
            return false;
        }
        SceneLocation Candidate(int index,double[] target,double scale,double dx,double dy,string method) {
            SceneLocation c=new SceneLocation();c.Reference=index;c.Scale=scale;c.Dx=dx;c.Dy=dy;c.Method=method;c.Score=Score(references[index],target,scale,dx,dy);return c;
        }
        static void Keep(List<SceneLocation> list,SceneLocation c,int capacity) {
            if(c.Score<.45)return;
            int index=0;while(index<list.Count&&list[index].Score>=c.Score)index++;
            if(index>=capacity)return;list.Insert(index,c);if(list.Count>capacity)list.RemoveAt(capacity);
        }
        bool SameLocation(SceneLocation a,SceneLocation b) {
            // Compare the projected west-curve center, including zoom.
            RectangleF ar=Project(references[a.Reference].Area,a),br=Project(references[b.Reference].Area,b);
            double x=(ar.X+ar.Width/2-br.X-br.Width/2)*Width,y=(ar.Y+ar.Height/2-br.Y-br.Height/2)*Height;
            return Math.Abs(x)<10&&Math.Abs(y)<7;
        }
        SceneLocation Choose(List<SceneLocation> candidates,double[] target,double minimum) {
            candidates.Sort(delegate(SceneLocation a,SceneLocation b){return b.Score.CompareTo(a.Score);});
            SceneLocation best=null;
            foreach(SceneLocation c in candidates) {
                if(c.Score<minimum)break;
                if(!LandmarksMatch(references[c.Reference],target,c))continue;
                if(best==null){best=c;best.Found=true;}
                else if(c.Score>=best.Score-.025&&!SameLocation(best,c)){best.Found=false;best.Ambiguous=true;break;}
            }
            return best;
        }
        public SceneLocation Locate(double[] pixels,double minimum) {
            return Locate(pixels,minimum,false);
        }
        // Color tracking needs a wider translation search after small PTZ
        // moves. Existing train and ON/OFF callers keep their original search.
        public SceneLocation Locate(double[] pixels,double minimum,bool widerTranslationSearch) {
            return Locate(pixels,minimum,widerTranslationSearch,false);
        }
        // The color reader opts into the corrected candidate retention. Existing
        // ON/OFF and train callers retain their established matching behavior.
        public SceneLocation LocateColor(double[] pixels,double minimum) {
            return Locate(pixels,minimum,true,true);
        }
        SceneLocation Locate(double[] pixels,double minimum,bool widerTranslationSearch,bool validatedReacquisition) {
            double[] target=Blur(pixels);List<SceneLocation> local=new List<SceneLocation>();
            SceneLocation rejected=null;
            for(int ri=0;ri<references.Count;ri++) {
                double scale=last!=null&&last.Reference==ri?last.Scale:1;
                double cx=last!=null&&last.Reference==ri?last.Dx:0,cy=last!=null&&last.Reference==ri?last.Dy:0;
                int xr=widerTranslationSearch?24:(last!=null&&last.Reference==ri?4:12);
                int yr=widerTranslationSearch?18:(last!=null&&last.Reference==ri?4:6);
                int zoomRadius=widerTranslationSearch?2:0;
                for(int si=-zoomRadius;si<=zoomRadius;si++) {
                    double trialScale=scale+si*.01;
                    for(int dy=-yr;dy<=yr;dy++)for(int dx=-xr;dx<=xr;dx++)
                    {
                        SceneLocation c=Candidate(ri,target,trialScale,cx+dx,cy+dy,widerTranslationSearch?"reacquiring":"tracking");
                        if(validatedReacquisition&&(rejected==null||c.Score>rejected.Score))rejected=c;
                        if(widerTranslationSearch&&(c.Score<minimum||!LandmarksMatch(references[ri],target,c)))continue;
                        Keep(local,c,32);
                    }
                }
            }
            SceneLocation result=Choose(local,target,minimum);
            // A rough translation can resemble a zoomed scene. Search scales
            // as well when the independent patch agreement is only marginal.
            if(result!=null&&result.Found&&result.PatchMinimum>=.80){last=result;return result;}
            List<SceneLocation> coarse=new List<SceneLocation>();
            for(int ri=0;ri<references.Count;ri++)for(int si=0;si<=17;si++) {
                double scale=.65+si*.05;
                for(int dy=-48;dy<=48;dy+=4)for(int dx=-96;dx<=96;dx+=4) {
                    SceneLocation c=Candidate(ri,target,scale,dx,dy,"reacquired");
                    if(validatedReacquisition&&(rejected==null||c.Score>rejected.Score))rejected=c;
                    // The color reader must keep valid landmark matches before
                    // truncating its candidate list. High-scoring wrong zooms
                    // otherwise crowd the real camera position out of search.
                    if(validatedReacquisition&&(c.Score<minimum||!LandmarksMatch(references[ri],target,c)))continue;
                    Keep(coarse,c,24);
                }
            }
            List<SceneLocation> fine=new List<SceneLocation>();
            foreach(SceneLocation c in coarse)for(int si=-2;si<=2;si++) {
                double scale=c.Scale+si*.01;if(scale<.65||scale>1.5)continue;
                for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++) {
                    SceneLocation f=Candidate(c.Reference,target,scale,c.Dx+dx,c.Dy+dy,"reacquired");
                    if(validatedReacquisition&&(rejected==null||f.Score>rejected.Score))rejected=f;
                    if(validatedReacquisition&&(f.Score<minimum||!LandmarksMatch(references[f.Reference],target,f)))continue;
                    Keep(fine,f,48);
                }
            }
            // A marginal but validated local match remains a candidate. A failed
            // wider search must not erase it, and competing valid locations still
            // pass through the same ambiguity check before any colors are read.
            if(validatedReacquisition)fine.AddRange(local);
            result=Choose(fine,target,minimum);
            if(result!=null&&result.Found){last=result;return result;}
            SceneLocation missing=result??(fine.Count>0?fine[0]:(validatedReacquisition&&rejected!=null?rejected:new SceneLocation()));
            if(validatedReacquisition)missing.Found=false;
            missing.Method=missing.Ambiguous?"ambiguous":"searching";return missing;
        }
        public static RectangleF Project(RectangleF r,SceneLocation fit) {
            return new RectangleF((float)(r.X*fit.Scale+fit.Dx/Width),(float)(r.Y*fit.Scale+fit.Dy/Height),(float)(r.Width*fit.Scale),(float)(r.Height*fit.Scale));
        }
        public static RectangleF Unproject(RectangleF r,SceneLocation fit) {
            return new RectangleF((float)((r.X-fit.Dx/Width)/fit.Scale),(float)((r.Y-fit.Dy/Height)/fit.Scale),(float)(r.Width/fit.Scale),(float)(r.Height/fit.Scale));
        }
    }
}
