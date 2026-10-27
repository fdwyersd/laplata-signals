using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Media;
using System.Security.Cryptography;
using System.Xml.Serialization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

[assembly: AssemblyVersion("0.31.0.0")]
[assembly: AssemblyFileVersion("0.31.0.0")]

namespace LapWest {
    public static class SoundFile {
        public static bool IsPcmWave(string path) {
            try {using(BinaryReader r=new BinaryReader(File.OpenRead(path))) {
                if(r.BaseStream.Length<44||r.ReadUInt32()!=0x46464952)return false;
                r.ReadUInt32();if(r.ReadUInt32()!=0x45564157)return false;
                bool format=false,data=false;
                while(r.BaseStream.Position+8<=r.BaseStream.Length) {
                    uint id=r.ReadUInt32(),length=r.ReadUInt32();long next=r.BaseStream.Position+length+(length%2);
                    if(next>r.BaseStream.Length)return false;
                    if(id==0x20746D66) {
                        if(length<16)return false;
                        int tag=r.ReadUInt16(),channels=r.ReadUInt16();uint rate=r.ReadUInt32();r.ReadUInt32();int align=r.ReadUInt16(),bits=r.ReadUInt16();
                        format=tag==1&&(channels==1||channels==2)&&rate>=8000&&rate<=192000&&(bits==8||bits==16)&&align==channels*bits/8;
                    }else if(id==0x61746164)data=length>0;
                    r.BaseStream.Position=next;
                }
                return format&&data;
            }}catch{return false;}
        }
    }
    public class Settings {
        public string Url = "http://66.179.190.208/laplatasignal.jpg";
        public int PollSeconds = 60;
        public int StaleSeconds = 240;
        public int FlashSeconds = 10;
        public bool Sound = true;
        public bool Toast = true;
        public bool AlwaysOnTop = true;
        public bool ShowSearchBoxes = false;
        public DetectionBox M2Box=new DetectionBox(),M1Box=new DetectionBox();
        public DetectionBox SignalSearchBox=new DetectionBox(),SignalMeasureBox=new DetectionBox();
        public double RoiX = .585, RoiY = .19, RoiW = .18, RoiH = .10;
        public int RedMinimum = 30;
        public double OnFraction = .003, OffFraction = .001;
        public double SceneMinimum = .68;
        public double DaySkyLimit = 35;
        public int WindowX = -1, WindowY = -1;
        static bool InRange(double value,double min,double max) {return !Double.IsNaN(value)&&!Double.IsInfinity(value)&&value>=min&&value<=max;}
        public void Normalize() {
            if(M2Box==null)M2Box=new DetectionBox();if(M1Box==null)M1Box=new DetectionBox();
            if(SignalSearchBox==null)SignalSearchBox=new DetectionBox();if(SignalMeasureBox==null)SignalMeasureBox=new DetectionBox();
            M2Box.Normalize();M1Box.Normalize();SignalSearchBox.Normalize();SignalMeasureBox.Normalize();
            // Upgrade the previous default rectangle when carrying settings forward.
            if(RoiX==.60&&RoiY==.20&&RoiW==.15&&RoiH==.08){RoiX=.585;RoiY=.19;RoiW=.18;RoiH=.10;}
            // Correct the accidental camera-address edits in v0.21/v0.23 saved settings.
            if(Url=="http://66.179.190.218/laplatasignal.jpg"||Url=="http://66.179.190.238/laplatasignal.jpg")Url="http://66.179.190.208/laplatasignal.jpg";
            Uri u;if(!Uri.TryCreate(Url,UriKind.Absolute,out u)||(u.Scheme!="http"&&u.Scheme!="https"))Url="http://66.179.190.208/laplatasignal.jpg";
            // User requested one-minute polling, including upgraded saved settings.
            PollSeconds=60;
            if(StaleSeconds<180||StaleSeconds>7200)StaleSeconds=240;
            FlashSeconds=Math.Max(1,Math.Min(60,FlashSeconds));
            if(!InRange(RoiX,.04,.90)||!InRange(RoiY,.05,.90)||!InRange(RoiW,.01,.5)||!InRange(RoiH,.01,.5)||RoiX+RoiW>.96||RoiY+RoiH>.95) {RoiX=.585;RoiY=.19;RoiW=.18;RoiH=.10;}
            if(RedMinimum<10||RedMinimum>240)RedMinimum=30;
            if(!InRange(OnFraction,.0001,.5)||!InRange(OffFraction,0,.1)||OffFraction>=OnFraction) {OnFraction=.003;OffFraction=.001;}
            if(!InRange(SceneMinimum,.3,.98))SceneMinimum=.68;
            if(!InRange(DaySkyLimit,5,180))DaySkyLimit=35;
        }
    }
    public class Reading {
        public SignalColorReading SignalColors;
        public string State, Reason, Gate, LocationMethod;
        public bool LocationFound;
        public double SceneScale=1, LandmarkMinimum;
        public int LandmarkMatches;
        public RectangleF SearchRegion;
        public string TrainDisplayLabel="";
        public bool TrainSceneFound;
        public double TrainSceneScore=-1,TrainM1Score,TrainM2Score;
        public int TrainBodyPixels,TrainSpanX,TrainSpanY;
        public string TrainDecision="not_evaluated",TrainExample="",TrainAcceptanceMode="not_accepted";
        public string TrainAdditionalDecision="not_evaluated";
        public double TrainAdditionalScore;
        public string TrainViewDecision="not_evaluated";
        public double TrainViewM1Score,TrainViewM2Score;
        public double TrainMatchSceneScore=-1;
        public int TrainMatchBodyPixels;
        public int CurveWhitePixels;
        public int WhiteRailPixels,WhiteSearchPixels,WhiteComponents,WhiteRejectedShape,WhiteRejectedBoundary,WhiteRejectedResidual,WhiteRejectedLocation;
        public double WhiteRailFill,WhiteRailResidual,WhiteRailSlope;
        public string WhiteDecision="not_evaluated",WhitePattern="";
        public RectangleF WhiteSearchRegion;
        public RectangleF WhiteRailRegion;
        public double RedFraction, SceneCorrelation, Sky, ChangeFraction;
        public double ClockSunElevation;
        public bool DaytimeAllowed;
        public DateTime AnalysisUtc;
        public int RedPixels, ShiftX, ShiftY, SearchRed, MinimumPatternPixels, OtherColor, BrightPixels;
        public double BrightLimit;
        public int SampleWidth, SampleHeight, ViewIndex=-1;
        public int[] WatchRed=new int[4], WatchYellow=new int[4];
        public bool Changed;
        public bool ReflectionFound;
        public bool Ambiguous;
        public RectangleF Region, WatchRegion;
        public byte[] Crop;
    }
    public sealed class Detector : IDisposable {
        const int SW = 160, SH = 90;
        readonly Settings cfg;
        sealed class Reflection {public Rectangle Bounds;public double Score;public bool Ambiguous;}
        readonly SceneLocator locator=new SceneLocator();
        readonly Dictionary<int,RectangleF> trackedRegions=new Dictionary<int,RectangleF>();
        RectangleF lastDisplayRegion=RectangleF.Empty,lastDisplayWatch=RectangleF.Empty;
        Rectangle lastSamplePixels=Rectangle.Empty;
        Size lastSampleSize=Size.Empty;
        public Detector(Settings settings, Bitmap off) { cfg = settings; SetReference(off); }
        public static Bitmap Scale(Bitmap src, int w, int h, bool nearest) {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(b)) {
                g.InterpolationMode = nearest ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0,0,w,h), 0,0,src.Width,src.Height,GraphicsUnit.Pixel);
            }
            return b;
        }
        static double Gray(Color c) { return .299*c.R + .587*c.G + .114*c.B; }
        public void ResetTracking() {locator.Reset();trackedRegions.Clear();lastDisplayRegion=RectangleF.Empty;lastDisplayWatch=RectangleF.Empty;lastSamplePixels=Rectangle.Empty;lastSampleSize=Size.Empty;}
        RectangleF DefaultArea() {return new RectangleF((float)cfg.RoiX,(float)cfg.RoiY,(float)cfg.RoiW,(float)cfg.RoiH);}
        RectangleF ReferenceAreaFor(Bitmap off) {
            if(locator.Count==0)return DefaultArea();
            SceneLocation fit=locator.Locate(SceneLocator.Thumbnail(off),cfg.SceneMinimum);
            if(!fit.Found)throw new Exception("Cannot confirm the west-facing background for this OFF reference.");
            RectangleF area=SceneLocator.Project(locator.ReferenceArea(fit.Reference),fit);
            if(!new RectangleF(0,0,1,1).Contains(area))throw new Exception("West-curve area is outside the proposed OFF reference.");
            return area;
        }
        public void SetReference(Bitmap off) {RectangleF area=ReferenceAreaFor(off);locator.SetReference(off,area);ResetTracking();}
        public Detector WithReference(Bitmap off) {
            RectangleF area=ReferenceAreaFor(off);Detector candidate=new Detector(cfg,off);
            candidate.locator.SetReference(off,area);return candidate;
        }
        public void AddSceneReference(Bitmap scene) {locator.AddReference(scene,DefaultArea());}
        bool IsReflectionRed(Color c) {
            return c.R>=cfg.RedMinimum&&c.R>1.3*c.G+10&&c.R>1.15*c.B+6&&c.B>=c.G*.65;
        }
        bool WhiteRailPattern(Reading r,Bitmap source,SceneLocation fit) {
            int frameWidth=source.Width,frameHeight=source.Height;
            // Use the aligned curve search context as well as the visible box.
            // A previous tight signal crop must not clip an approaching flare.
            RectangleF area=RectangleF.Union(r.WatchRegion,r.SearchRegion);
            area.Inflate((float)(.015*fit.Scale),(float)(.03*fit.Scale));area=RectangleF.Intersect(area,new RectangleF(0,0,1,1));
            r.WhiteSearchRegion=area;
            RectangleF curveArea=cfg.SignalMeasureBox.Enabled?r.Region:SceneLocator.Project(locator.ReferenceArea(fit.Reference),fit);
            int left=Math.Max(0,(int)(area.Left*frameWidth)),top=Math.Max(0,(int)(area.Top*frameHeight));
            int width=Math.Min(frameWidth-left,Math.Max(2,(int)(area.Width*frameWidth))),height=Math.Min(frameHeight-top,Math.Max(2,(int)(area.Height*frameHeight)));
            bool[] mask=new bool[width*height];int[] queue=new int[mask.Length];
            for(int i=0;i<mask.Length;i++) {
                Color c=source.GetPixel(left+i%width,top+i/width);int rr=c.R,gg=c.G,bb=c.B;
                int high=Math.Max(rr,Math.Max(gg,bb)),low=Math.Min(rr,Math.Min(gg,bb));
                double level=.299*rr+.587*gg+.114*bb,reference;
                mask[i]=level>95&&bb>55&&high-low<75&&locator.ReferenceWhitePeak(fit,(left+i%width)*SW/(double)frameWidth,(top+i/width)*SH/(double)frameHeight,out reference)&&level>reference+35&&(reference<70||level>reference*1.4+25);
                if(mask[i]) {
                    r.WhiteSearchPixels++;
                    if(curveArea.Contains((float)(left+i%width)/frameWidth,(float)(top+i/width)/frameHeight))r.CurveWhitePixels++;
                }
            }
            int curveMinimum=Math.Max(8,(int)(curveArea.Width*frameWidth*curveArea.Height*frameHeight*.005));
            if(r.CurveWhitePixels<curveMinimum) {r.WhiteDecision="no_bright_obstruction_at_curve";return false;}
            r.WhiteDecision=r.WhiteSearchPixels==0?"no_new_white_pixels":"no_rail_shape";
            int minimum=Math.Max(8,(int)(frameWidth*frameHeight*.00004));double best=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
                int seed=y*width+x;if(!mask[seed])continue;
                int head=0,tail=0,minX=x,maxX=x,minY=y,maxY=y;double sx=0,sy=0,sxx=0,syy=0,sxy=0;
                queue[tail++]=seed;mask[seed]=false;
                while(head<tail) {
                    int p=queue[head++],px=p%width,py=p/width;
                    minX=Math.Min(minX,px);maxX=Math.Max(maxX,px);minY=Math.Min(minY,py);maxY=Math.Max(maxY,py);
                    sx+=px;sy+=py;sxx+=(double)px*px;syy+=(double)py*py;sxy+=(double)px*py;
                    for(int yy=Math.Max(0,py-2);yy<=Math.Min(height-1,py+2);yy++)for(int xx=Math.Max(0,px-2);xx<=Math.Min(width-1,px+2);xx++) {
                        int k=yy*width+xx;if(mask[k]){mask[k]=false;queue[tail++]=k;}
                    }
                }
                int w=maxX-minX+1,h=maxY-minY+1;r.WhiteComponents++;
                if(tail<minimum||w<Math.Max(12,frameWidth*.025)||w<h*3.5){r.WhiteRejectedShape++;continue;}
                if(minY==0||maxY==height-1){r.WhiteRejectedBoundary++;continue;}
                double vx=sxx-sx*sx/tail,cov=sxy-sx*sy/tail,vy=syy-sy*sy/tail;
                if(vx<1||Math.Abs(cov/vx)>.7){r.WhiteRejectedShape++;continue;}
                double residual=Math.Sqrt(Math.Max(0,vy-cov*cov/vx)/tail),fill=(double)tail/(w*h);
                bool branched=residual>Math.Max(2.5,frameHeight*.009);
                // Allow a modestly wider spread for sparse, branched rail
                // reflections. Solid broad glare does not get this allowance.
                if(branched&&(residual>Math.Max(3.5,frameHeight*.012)||fill>.55)){r.WhiteRejectedResidual++;continue;}
                RectangleF bounds=new RectangleF((float)(left+minX)/frameWidth,(float)(top+minY)/frameHeight,(float)w/frameWidth,(float)h/frameHeight);
                if(!bounds.IntersectsWith(curveArea)){r.WhiteRejectedLocation++;continue;}
                double score=tail*Math.Sqrt(w);if(score<=best)continue;best=score;r.WhiteRailPixels=tail;
                r.WhiteRailRegion=bounds;r.WhiteRailFill=fill;r.WhiteRailResidual=residual;r.WhiteRailSlope=cov/vx;
                r.WhiteDecision="rail_pattern";r.WhitePattern=branched?"branched_reflection":"narrow_reflection";
            }
            return best>0;
        }
        Reflection FindReflection(Bitmap b,RectangleF area,out int searchRed,out int minimumPixels) {
            int w=b.Width,h=b.Height;
            int left=Math.Max(1,(int)(area.Left*w)),right=Math.Min(w-2,(int)(area.Right*w));
            int top=Math.Max(1,(int)(area.Top*h)),bottom=Math.Min(h-2,(int)(area.Bottom*h));
            bool[] mask=new bool[w*h];searchRed=0;minimumPixels=Math.Max(8,(int)(w*h*.000018));
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)if(IsReflectionRed(b.GetPixel(x,y))){mask[y*w+x]=true;searchRed++;}
            if(right<left||bottom<top)return null;
            int[] queue=new int[Math.Max(1,(right-left+1)*(bottom-top+1))];Reflection best=null,runner=null;
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++) {
                int seed=y*w+x;if(!mask[seed])continue;
                int head=0,tail=0,minX=x,maxX=x,minY=y,maxY=y;double sx=0,sy=0,sxx=0,syy=0,sxy=0;
                queue[tail++]=seed;mask[seed]=false;
                while(head<tail) {
                    int p=queue[head++],px=p%w,py=p/w;
                    minX=Math.Min(minX,px);maxX=Math.Max(maxX,px);minY=Math.Min(minY,py);maxY=Math.Max(maxY,py);
                    sx+=px;sy+=py;sxx+=(double)px*px;syy+=(double)py*py;sxy+=(double)px*py;
                    // Bridge tiny compression gaps along a thin reflection.
                    for(int yy=Math.Max(top,py-2);yy<=Math.Min(bottom,py+2);yy++)for(int xx=Math.Max(left,px-3);xx<=Math.Min(right,px+3);xx++) {
                        int k=yy*w+xx;if(mask[k]){mask[k]=false;queue[tail++]=k;}
                    }
                }
                int width=maxX-minX+1,height=maxY-minY+1;
                if(tail<minimumPixels||width<Math.Max(8,(int)(w*.009))||width>w*.20||width<height*2.1)continue;
                double vx=sxx-sx*sx/tail,cov=sxy-sx*sy/tail,vy=syy-sy*sy/tail;
                if(vx<1||Math.Abs(cov/vx)>.5)continue;
                double residual=Math.Sqrt(Math.Max(0,vy-cov*cov/vx)/tail);
                if(residual>Math.Max(2.5,h*.006))continue;
                Reflection found=new Reflection();found.Bounds=Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);found.Score=tail*Math.Sqrt(width);
                if(best==null||found.Score>best.Score){runner=best;best=found;}
                else if(runner==null||found.Score>runner.Score)runner=found;
            }
            if(best!=null&&runner!=null&&runner.Score>best.Score*.75) {
                double dx=(best.Bounds.Left+best.Bounds.Right-runner.Bounds.Left-runner.Bounds.Right)/2.0;
                double dy=(best.Bounds.Top+best.Bounds.Bottom-runner.Bounds.Top-runner.Bounds.Bottom)/2.0;
                best.Ambiguous=Math.Abs(dx)>w*.12||Math.Abs(dy)>h*.05;
            }
            return best;
        }
        static RectangleF DisplayWatch(RectangleF region,double scale) {
            float ww=Math.Min(.60f,Math.Max((float)(.22*scale),region.Width)),wh=Math.Min(.40f,Math.Max((float)(.12*scale),region.Height));
            return new RectangleF(Math.Max(0,Math.Min(1-ww,region.X+region.Width/2-ww/2)),Math.Max(0,Math.Min(1-wh,region.Y+region.Height/2-wh/2)),ww,wh);
        }
        public Reading Analyze(Bitmap frame, byte[] previousCrop) {return Analyze(frame,previousCrop,DateTime.UtcNow);}
        public Reading Analyze(Bitmap frame, byte[] previousCrop,DateTime utc) {return Analyze(frame,previousCrop,utc,null);}
        public Reading Analyze(Bitmap frame, byte[] previousCrop,DateTime utc,BoxAlignment manualAlignment) {
            Reading r = new Reading();
            r.AnalysisUtc=utc;r.ClockSunElevation=SolarClock.SunElevation(utc);
            r.DaytimeAllowed=r.ClockSunElevation>=SolarClock.DaylightMinimumElevation;
            r.Region=lastDisplayRegion.IsEmpty?new RectangleF((float)cfg.RoiX,(float)cfg.RoiY,(float)cfg.RoiW,(float)cfg.RoiH):lastDisplayRegion;
            r.WatchRegion=lastDisplayWatch.IsEmpty?new RectangleF((float)(cfg.RoiX+cfg.RoiW/2-.11),(float)(cfg.RoiY+cfg.RoiH/2-.06),.22f,.12f):lastDisplayWatch;
            double[] t=SceneLocator.Thumbnail(frame);
            double sky=0;int skyN=0;
            for(int y=5;y<15;y++)for(int x=48;x<96;x++){sky+=t[y*SW+x];skyN++;}
            r.Sky=sky/skyN;r.SceneCorrelation=-1;
            SceneLocation location=locator.Locate(t,cfg.SceneMinimum);
            double best=location.Score;
            r.SceneCorrelation=best;r.ShiftX=(int)Math.Round(location.Dx);r.ShiftY=(int)Math.Round(location.Dy);r.ViewIndex=location.Reference;
            r.SceneScale=location.Scale;r.LandmarkMatches=location.Landmarks;r.LandmarkMinimum=location.PatchMinimum;r.LocationMethod=location.Method;r.LocationFound=location.Found;
            if(!location.Found) {
                // Keep the visible last-known box, but discard its measurement
                // lock so a return from a different PTZ view is a fresh fit.
                trackedRegions.Clear();locator.Reset();
                r.State="CHECK IMAGE";r.Gate=location.Ambiguous?"scene_ambiguous":"scene_mismatch";
                r.Reason=location.Ambiguous?"PTZ view is ambiguous; the west-curve location cannot be confirmed.":"PTZ view is outside the recognized west-curve range. Waiting for the camera to return.";
                lastSamplePixels=Rectangle.Empty;lastSampleSize=Size.Empty;return r;
            }
            RectangleF baseRegion=locator.ReferenceArea(location.Reference);
            RectangleF expected=SceneLocator.Project(baseRegion,location);
            RectangleF tracked;
            r.Region=trackedRegions.TryGetValue(location.Reference,out tracked)?SceneLocator.Project(tracked,location):expected;
            r.SearchRegion=SceneLocator.Project(new RectangleF(baseRegion.X-.16f,baseRegion.Y-.08f,baseRegion.Width+.28f,baseRegion.Height+.16f),location);
            if(cfg.SignalSearchBox.Enabled||cfg.SignalMeasureBox.Enabled) {
                if(manualAlignment==null||!manualAlignment.Located) {
                    r.LocationFound=false;r.State="CHECK IMAGE";r.Gate="manual_box_alignment";
                    r.Reason="Saved detection boxes cannot be aligned with this view. Waiting for a recognized west-curve view.";
                    lastSamplePixels=Rectangle.Empty;lastSampleSize=Size.Empty;return r;
                }
                if(cfg.SignalSearchBox.Enabled)r.SearchRegion=manualAlignment.ToFrame(cfg.SignalSearchBox.Area);
                if(cfg.SignalMeasureBox.Enabled)r.Region=manualAlignment.ToFrame(cfg.SignalMeasureBox.Area);
            }
            if(!new RectangleF(0,0,1,1).Contains(r.Region)||(cfg.SignalSearchBox.Enabled&&!new RectangleF(0,0,1,1).Contains(r.SearchRegion))) {
                trackedRegions.Clear();locator.Reset();lastSamplePixels=Rectangle.Empty;lastSampleSize=Size.Empty;
                r.LocationFound=false;r.LocationMethod="out of view";r.State="CHECK IMAGE";r.Gate="outside_view";
                r.Reason="West-curve watch area is outside this picture; waiting for the camera to return.";return r;
            }
            // Locate before interpreting brightness: a station close-up can
            // put a floodlit building in the old fixed sky patch at night.
            RectangleF skyArea=SceneLocator.Project(new RectangleF(48f/SW,5f/SH,48f/SW,10f/SH),location);
            RectangleF visibleSky=RectangleF.Intersect(skyArea,new RectangleF(0,0,1,1));
            r.WatchRegion=DisplayWatch(r.Region,location.Scale);
            if(visibleSky.Width*visibleSky.Height<skyArea.Width*skyArea.Height*.75f) {
                trackedRegions.Clear();locator.Reset();lastSamplePixels=Rectangle.Empty;lastSampleSize=Size.Empty;
                r.LocationFound=false;r.State="CHECK IMAGE";r.Gate="outside_view";
                r.Reason="PTZ view clips the expected sky area; waiting for the west-curve view.";return r;
            }
            sky=0;skyN=0;
            for(int yy=Math.Max(0,(int)(visibleSky.Top*SH));yy<Math.Min(SH,(int)(visibleSky.Bottom*SH));yy++)
                for(int xx=Math.Max(0,(int)(visibleSky.Left*SW));xx<Math.Min(SW,(int)(visibleSky.Right*SW));xx++){sky+=t[yy*SW+xx];skyN++;}
            r.Sky=sky/Math.Max(1,skyN);
            if(r.Sky>cfg.DaySkyLimit) {
                r.State=r.DaytimeAllowed?"DAYLIGHT / DISARMED":"CHECK IMAGE";
                r.Gate=r.DaytimeAllowed?"daylight":"bright_at_night";
                r.Reason=r.DaytimeAllowed?"Located sky is bright and the time supports daylight at La Plata.":"Unexpected brightness at night; inspect the camera framing or lighting.";
                lastSamplePixels=Rectangle.Empty;lastSampleSize=Size.Empty;return r;
            }
            // Retain native detail up to 1280 pixels wide; never enlarge a small feed.
            int W=Math.Min(1280,frame.Width),H=(int)Math.Round((double)frame.Height*W/frame.Width);
            Reflection reflection;int searchRed,minimumPixels;
            using(Bitmap search=Scale(frame,W,H,false))reflection=FindReflection(search,r.SearchRegion,out searchRed,out minimumPixels);
            r.SearchRed=searchRed;r.MinimumPatternPixels=minimumPixels;r.Ambiguous=reflection!=null&&reflection.Ambiguous;
            r.SampleWidth=W;r.SampleHeight=H;
            r.ReflectionFound=reflection!=null&&!reflection.Ambiguous&&best>=cfg.SceneMinimum;
            if(r.ReflectionFound&&cfg.SignalMeasureBox.Enabled) {
                Rectangle bb=reflection.Bounds;
                RectangleF reflectionArea=new RectangleF((float)bb.X/W,(float)bb.Y/H,(float)bb.Width/W,(float)bb.Height/H);
                r.ReflectionFound=reflectionArea.IntersectsWith(r.Region);
            }
            if(r.ReflectionFound&&!cfg.SignalMeasureBox.Enabled) {
                Rectangle bb=reflection.Bounds;
                double width=Math.Max(.07,Math.Min(cfg.RoiW,(double)bb.Width/W+.04));
                double height=Math.Max(.035,Math.Min(cfg.RoiH,(double)bb.Height/H+.025));
                double cx=(bb.Left+bb.Width/2.0)/W,cy=(bb.Top+bb.Height/2.0)/H;
                r.Region=new RectangleF((float)Math.Max(0,Math.Min(1-width,cx-width/2)),(float)Math.Max(0,Math.Min(1-height,cy-height/2)),(float)width,(float)height);
            }
            // Wider context includes nearby upper/lower reflection groups. Keep
            // the established inner measurement area for brightness/change gates.
            r.WatchRegion=DisplayWatch(r.Region,location.Scale);
            lastDisplayRegion=r.Region;lastDisplayWatch=r.WatchRegion;
            int x0=(int)(r.Region.X*W),y0=(int)(r.Region.Y*H),rw=Math.Max(2,(int)(r.Region.Width*W)),rh=Math.Max(2,(int)(r.Region.Height*H));
            if(x0<0||y0<0||x0+rw>=W||y0+rh>=H) {r.State="CHECK IMAGE";r.Gate="outside_view";r.Reason="Watch area is outside this view.";return r;}
            Rectangle samplePixels=new Rectangle(x0,y0,rw,rh);Size sampleSize=new Size(W,H);
            if(previousCrop!=null&&(samplePixels!=lastSamplePixels||sampleSize!=lastSampleSize))previousCrop=null;
            lastSamplePixels=samplePixels;lastSampleSize=sampleSize;
            r.Crop=new byte[rw*rh*3]; int npx=0, red=0;
            int otherColor=0, bright=0, changed=0, compared=0;
            using (Bitmap b=Scale(frame,W,H,false)) {
                RectangleF watch=r.WatchRegion;
                int wl=(int)(watch.X*W),wt=(int)(watch.Y*H),wr=Math.Min(W,(int)((watch.X+watch.Width)*W)),wb=Math.Min(H,(int)((watch.Y+watch.Height)*H));
                for(int yy=wt;yy<wb;yy++)for(int xx=wl;xx<wr;xx++) {
                    Color c=b.GetPixel(xx,yy);int q=(yy>=(wt+wb)/2?2:0)+(xx>=(wl+wr)/2?1:0);
                    if(IsReflectionRed(c))r.WatchRed[q]++;
                    else if(c.R>50&&c.G>30&&c.R>=c.G*.9&&c.R<c.G*2.6&&c.G>c.B*1.5+10)r.WatchYellow[q]++;
                }
                for(int y=0;y<rh;y++) for(int x=0;x<rw;x++) {
                    Color c=b.GetPixel(x0+x,y0+y); int k=(y*rw+x)*3;
                    r.Crop[k]=c.R;r.Crop[k+1]=c.G;r.Crop[k+2]=c.B;
                    // Ignore pure yellow annotation pixels in supplied samples.
                    bool annotation=c.R>170 && c.G>170 && c.B<100;
                    bool previousAnnotation=previousCrop!=null && previousCrop.Length==r.Crop.Length &&
                        previousCrop[k]>170 && previousCrop[k+1]>170 && previousCrop[k+2]<100;
                    if(annotation) continue;
                    npx++;
                    bool isRed=IsReflectionRed(c);
                    if(isRed)red++;
                    else if(c.G>40 && c.G>1.25*c.R+10 && c.G>1.15*c.B+6) otherColor++;
                    else if(c.R>45 && c.G>30 && c.R>c.G*1.05 && c.G>c.B*1.5+10) otherColor++;
                    if(!isRed&&Gray(c)>75) bright++;
                    if(previousCrop!=null && previousCrop.Length==r.Crop.Length && !previousAnnotation) {
                        int delta=Math.Max(Math.Abs(c.R-previousCrop[k]),Math.Max(Math.Abs(c.G-previousCrop[k+1]),Math.Abs(c.B-previousCrop[k+2])));
                        if(delta>30) changed++;compared++;
                    }
                }
            }
            r.RedPixels=red;r.RedFraction=(double)red/Math.Max(1,npx);
            r.OtherColor=otherColor;r.BrightPixels=bright;r.BrightLimit=baseRegion.Width*W*baseRegion.Height*H*.08*location.Scale*location.Scale;
            r.ChangeFraction=(double)changed/Math.Max(1,compared);
            r.Changed=compared>0 && r.ChangeFraction>=.06;
            if(!r.LocationFound) {r.State="CHECK IMAGE";r.Gate="scene_mismatch";r.Reason=String.Format(CultureInfo.InvariantCulture,"View match {0:F2} is below {1:F2}; camera view cannot be confirmed.",best,cfg.SceneMinimum);}
            // Keep the original physical brightness limit when the tracking
            // window tightens around a legitimate bright reflection.
            else if(bright>r.BrightLimit) {r.State="CHECK IMAGE";r.Gate="bright_obstruction";r.Reason="Bright light or obstruction in watch area.";}
            else if(reflection!=null&&reflection.Ambiguous) {r.State="CHECK IMAGE";r.Gate="ambiguous_patterns";r.Reason="Multiple separated reflection candidates; inspect picture.";}
            else if(r.ReflectionFound&&r.RedFraction>=cfg.OnFraction) {r.State="SIGNALS ON";r.Gate="reflection_on";r.Reason="Red rail reflection found in the located west-curve area.";trackedRegions[location.Reference]=SceneLocator.Unproject(r.Region,location);}
            else if(r.RedFraction<=cfg.OffFraction && otherColor<4&&searchRed<minimumPixels) {r.State="SIGNALS OFF";r.Gate="reflection_off";r.Reason="No matching signal reflection detected in the west-curve search area.";}
            else {r.State="CHECK IMAGE";r.Gate="uncertain_color_or_pattern";r.Reason="Faint or different-colored light; inspect picture.";}
            if(r.State=="CHECK IMAGE"&&r.LocationFound&&r.Gate=="bright_obstruction") {
                using(Bitmap context=Scale(frame,W,H,false))if(WhiteRailPattern(r,context,location))r.TrainDisplayLabel="WEST CURVE TRAIN";
            }
            return r;
        }
        public void Dispose() { }
    }
    public static class ToastPlacement {
        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]
        struct AppBarData {
            public uint Size;public IntPtr Window;public uint Callback,Edge;
            public NativeRect Rect;public IntPtr Parameter;
        }
        [DllImport("shell32.dll",CallingConvention=CallingConvention.StdCall)]
        static extern UIntPtr SHAppBarMessage(uint message,ref AppBarData data);
        public static Rectangle WorkArea(out int edge) {
            edge=3;
            if(Environment.OSVersion.Platform==PlatformID.Win32NT)try {
                AppBarData data=new AppBarData();data.Size=(uint)Marshal.SizeOf(typeof(AppBarData));
                if(SHAppBarMessage(5,ref data)!=UIntPtr.Zero&&data.Rect.Right>data.Rect.Left&&data.Rect.Bottom>data.Rect.Top) {
                    Rectangle bar=Rectangle.FromLTRB(data.Rect.Left,data.Rect.Top,data.Rect.Right,data.Rect.Bottom);
                    Screen host=Screen.FromRectangle(bar);edge=TaskbarEdge(bar,host.Bounds);return host.WorkingArea;
                }
            }catch{}
            return Screen.PrimaryScreen.WorkingArea;
        }
        public static int TaskbarEdge(Rectangle bar,Rectangle screen) {
            if(bar.Width>=bar.Height)return bar.Top+bar.Height/2<screen.Top+screen.Height/2?1:3;
            return bar.Left+bar.Width/2<screen.Left+screen.Width/2?0:2;
        }
        public static Point Location(Rectangle workArea,Size size,int edge) {
            int x=edge==0?workArea.Left+12:workArea.Right-size.Width-12;
            int y=edge==1?workArea.Top+12:workArea.Bottom-size.Height-12;
            return new Point(Math.Max(workArea.Left,Math.Min(x,workArea.Right-size.Width)),Math.Max(workArea.Top,Math.Min(y,workArea.Bottom-size.Height)));
        }
    }
    // A small, nonactivating popup works with the same Windows 7 framework as
    // the dashboard and does not depend on the newer Windows notification API.
    sealed class SignalToast : Form {
        readonly System.Windows.Forms.Timer lifetime = new System.Windows.Forms.Timer();
        readonly Label heading = new Label(), stateLabel = new Label(), details = new Label();
        readonly Color accent;
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get { CreateParams p=base.CreateParams;p.ExStyle|=0x08000000|0x00000080;return p; }
        }
        public SignalToast(string previous,string state,string stamp,bool test,Rectangle workArea,int taskbarEdge,Action openDashboard) {
            accent=state=="SIGNALS ON"?Color.FromArgb(255,113,94):state=="SIGNALS OFF"?Color.FromArgb(118,210,158):Color.FromArgb(245,195,89);
            AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
            ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
            BackColor=Color.FromArgb(24,33,43);ForeColor=Color.White;
            Font=new Font("Segoe UI",9);ClientSize=new Size(Math.Min(390,Math.Max(240,workArea.Width-24)),150);
            Location=ToastPlacement.Location(workArea,Size,taskbarEdge);
            heading.Text=(test?"TEST: ":"")+"La Plata west curve";Text=heading.Text+" - "+state;
            heading.SetBounds(18,12,ClientSize.Width-66,23);heading.Font=new Font("Segoe UI",11,FontStyle.Bold);Controls.Add(heading);
            stateLabel.Text=state;stateLabel.ForeColor=accent;stateLabel.Font=new Font("Segoe UI",state.Length>18?13:17,FontStyle.Bold);
            stateLabel.SetBounds(18,40,ClientSize.Width-36,33);Controls.Add(stateLabel);
            details.Text="Changed from "+previous+"\n"+stamp+"\nClick to open dashboard";
            details.SetBounds(18,80,ClientSize.Width-36,60);details.ForeColor=Color.FromArgb(193,207,221);Controls.Add(details);
            Label dismiss=new Label();dismiss.Text="X";dismiss.TextAlign=ContentAlignment.MiddleCenter;
            dismiss.SetBounds(ClientSize.Width-39,9,28,27);dismiss.Cursor=Cursors.Hand;Controls.Add(dismiss);
            dismiss.Click+=delegate{Close();};
            EventHandler open=delegate{Close();openDashboard();};
            Click+=open;heading.Click+=open;stateLabel.Click+=open;details.Click+=open;
            heading.Cursor=stateLabel.Cursor=details.Cursor=Cursors.Hand;
            lifetime.Interval=10000;lifetime.Tick+=delegate{Close();};Shown+=delegate{lifetime.Start();};
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);using(Brush b=new SolidBrush(accent))e.Graphics.FillRectangle(b,0,0,5,ClientSize.Height);
            using(Pen p=new Pen(Color.FromArgb(72,88,105)))e.Graphics.DrawRectangle(p,0,0,ClientSize.Width-1,ClientSize.Height-1);
        }
        protected override void Dispose(bool disposing) {if(disposing)lifetime.Dispose();base.Dispose(disposing);}
    }
    sealed class FetchResult {
        public Bitmap Image;
        public byte[] SourceBytes;
        public string Hash;
        public DateTime? ModifiedUtc;
        public DateTime DownloadedUtc;
    }
    public sealed partial class WatchForm : Form {
        const int CheckImageFrames=4;
        const int SourceUpdateSeconds=60;
        readonly string root, dataPath, settingsPath;
        Settings cfg;
        Detector detector;
        TrainDisplayClassifier trainDisplay;
        SignalColorDisplay signalColors;
        readonly Label colorM2=new Label(),colorM1=new Label();
        readonly BackgroundWorker fetch = new BackgroundWorker();
        readonly object requestLock=new object();
        HttpWebRequest activeRequest;
        int streamGeneration=0,fetchGeneration=0;
        readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();
        readonly Label status = new Label(), reason = new Label(), times = new Label(), stats = new Label(), count = new Label();
        readonly PictureBox picture = new PictureBox();
        readonly ToolStripMenuItem showSearchBoxes=new ToolStripMenuItem("Show boxes");
        readonly Button boxesToggle;
        readonly ListBox history = new ListBox();
        readonly TextBox urlBox = new TextBox();
        readonly CheckBox sound = new CheckBox(), top = new CheckBox();
        readonly CheckBox toastOption = new CheckBox();
        SignalToast notification;
        string lastSignalState="";
        int checkImageCount;
        bool checkImagePending;
        string pendingCheckReason="";
        readonly Button pause = new Button();
        readonly ToolTip tips = new ToolTip();
        readonly Color background=Color.FromArgb(19,25,32), muted=Color.FromArgb(163,178,193);
        Color statusColor=Color.Silver;
        DateTime nextPoll=DateTime.MinValue, changedUtc=DateTime.MinValue, lastPollUtc=DateTime.MinValue, sameSince=DateTime.MinValue, flashUntil=DateTime.MinValue;
        DateTime? imageModifiedUtc;
        string lastHash="", currentState="", detail="Waiting for first image.", mode="LIVE";
        bool paused=false, sceneLatched=false;
        volatile bool closing=false;
        byte[] previousCrop;
        Bitmap currentImage;
        Reading reading;
        SoundPlayer player;
        TimeZoneInfo central;
        DiagnosticLog diagnostics;
        bool savedOffActive,curveReferenceActive;
        string diagnosticError="",frameId="",frameHash="";
        string decisionBefore="",rawState="",alertDecision="none",soundDecision="not requested",toastDecision="not requested";
        int decisionCountBefore;
        bool decisionFresh;
        readonly Button exportLog=new Button();
        public static void Run(string folder) {
            using(WatchForm form=new WatchForm(folder)) Application.Run(form);
        }
        public WatchForm(string folder) {
            root=folder;dataPath=ChooseDataPath(root);
            settingsPath=Path.Combine(dataPath,"settings.xml");cfg=LoadSettings();
            try {diagnostics=new DiagnosticLog(Path.Combine(dataPath,"diagnostics"));}
            catch(Exception ex){diagnosticError=ex.Message;}
            try { central=TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }catch {central=null;}
            string refFile=Path.Combine(dataPath,"reference-off.png"),startupNote="";
            using(Bitmap supplied=new Bitmap(Path.Combine(root,"samples/off.png")))detector=new Detector(cfg,supplied);
            string sceneFile=Path.Combine(root,"samples/curve-view.jpg");
            try {if(File.Exists(sceneFile))using(Bitmap scene=new Bitmap(sceneFile)){detector.AddSceneReference(scene);curveReferenceActive=true;}}
            catch {startupNote+="Additional curve reference unavailable; using the OFF reference. ";}
            if(File.Exists(refFile))try {
                using(Bitmap saved=new Bitmap(refFile))detector.SetReference(saved);
                savedOffActive=true;
                // SetReference carries the fitted area into the new reference.
                // Keep the supplied scene available for later reacquisition.
                bool restoreCurve=curveReferenceActive;curveReferenceActive=false;
                if(restoreCurve)using(Bitmap scene=new Bitmap(sceneFile)){detector.AddSceneReference(scene);curveReferenceActive=true;}
            }catch {startupNote+="Saved OFF reference could not be validated; using the supplied scene references. ";}
            try {signalColors=new SignalColorDisplay(root,cfg);}
            catch {startupNote+="Signal color calibration unavailable; color bar will show unknown. ";}
            string wav=Path.Combine(root,"alert.wav");
            try {trainDisplay=new TrainDisplayClassifier(cfg,root);if(trainDisplay.AdditionalExampleStatus!="ready")startupNote+="Additional M2 example unavailable; baseline train recognition remains active. ";if(trainDisplay.CurrentViewStatus!="ready")startupNote+="Current-view train examples unavailable; prior recognition remains active. ";}
            catch {startupNote+="Wider-picture train calibration unavailable; signal monitoring remains active. ";}
            try {if(File.Exists(wav)){if(!SoundFile.IsPcmWave(wav))throw new InvalidDataException("Sound is not a supported PCM WAV.");player=new SoundPlayer(wav);player.Load();}}
            catch {if(player!=null)player.Dispose();player=null;startupNote+="Custom sound unavailable; using system sound. ";}
            Text="LAP West Curve | Night Signal Watch v0.31";
            AutoScaleMode=AutoScaleMode.None;
            ClientSize=new Size(654,Math.Min(723,Math.Max(450,Screen.PrimaryScreen.WorkingArea.Height-80)));
            AutoScroll=true;AutoScrollMinSize=new Size(654,723);
            MinimumSize=new Size(500,400);
            BackColor=background;ForeColor=Color.White;Font=new Font("Segoe UI",9);
            FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=true;
            if(cfg.WindowX>=0 && cfg.WindowY>=0) {
                Rectangle saved=new Rectangle(cfg.WindowX,cfg.WindowY,Width,Height);bool visible=false;
                foreach(Screen s in Screen.AllScreens) if(s.WorkingArea.IntersectsWith(saved)) visible=true;
                if(visible) {StartPosition=FormStartPosition.Manual;Location=saved.Location;}
            }
            Label title=LabelAt("LA PLATA  /  WEST CURVE",12,9,300,27,15);title.Font=new Font("Segoe UI",15,FontStyle.Bold);
            boxesToggle=ButtonAt(cfg.ShowSearchBoxes?"Hide boxes":"Show boxes",321,8,102);
            boxesToggle.Click+=delegate{showSearchBoxes.Checked=!showSearchBoxes.Checked;};
            tips.SetToolTip(boxesToggle,"Show or hide all debugging boxes and M1/M2 labels on the camera picture. Recognition and the color status bar keep running. Your choice is saved.");
            exportLog.SetBounds(431,8,106,27);exportLog.Text="Export log";exportLog.ForeColor=Color.Black;exportLog.BackColor=Color.FromArgb(222,229,237);exportLog.UseVisualStyleBackColor=false;Controls.Add(exportLog);
            exportLog.Click+=delegate{ExportDecisions();};
            tips.SetToolTip(exportLog,"Save a ZIP of decision logs, recent camera snapshots, and scene references to share for troubleshooting.");
            toastOption.SetBounds(548,12,94,23);toastOption.Text="Toast";toastOption.Checked=cfg.Toast;Controls.Add(toastOption);
            tips.SetToolTip(toastOption,"Show a 10-second popup for every confirmed status change. CHECK IMAGE needs 4 fresh images. Initial status is silent.");
            toastOption.CheckedChanged+=delegate{cfg.Toast=toastOption.Checked;if(!cfg.Toast)CloseToast();SaveSettings();};
            status.SetBounds(12,43,630,37);status.Font=new Font("Segoe UI",19,FontStyle.Bold);status.Text="CONNECTING...";status.TextAlign=ContentAlignment.MiddleLeft;Controls.Add(status);
            reason.SetBounds(12,85,630,35);reason.ForeColor=muted;Controls.Add(reason);
            times.SetBounds(12,119,630,47);times.Font=new Font("Segoe UI",8.5f);times.ForeColor=muted;Controls.Add(times);
            picture.SetBounds(12,169,630,318);picture.SizeMode=PictureBoxSizeMode.Zoom;picture.BackColor=Color.Black;Controls.Add(picture);
            picture.Paint+=PaintRegion;
            ContextMenuStrip imageMenu=new ContextMenuStrip();
            showSearchBoxes.CheckOnClick=true;showSearchBoxes.Checked=cfg.ShowSearchBoxes;
            showSearchBoxes.CheckedChanged+=delegate{cfg.ShowSearchBoxes=showSearchBoxes.Checked;if(!cfg.ShowSearchBoxes&&editingBoxes)SetBoxEditing(false);boxesToggle.Text=cfg.ShowSearchBoxes?"Hide boxes":"Show boxes";SaveSettings();picture.Invalidate();};
            imageMenu.Items.Add(showSearchBoxes);picture.ContextMenuStrip=imageMenu;
            colorM2.SetBounds(12,491,313,27);colorM1.SetBounds(329,491,313,27);
            foreach(Label bar in new Label[]{colorM2,colorM1}) {bar.BackColor=Color.FromArgb(9,15,21);bar.Font=new Font("Segoe UI",9,FontStyle.Bold);bar.TextAlign=ContentAlignment.MiddleLeft;Controls.Add(bar);}
            colorM2.Text="M2: ?";colorM1.Text="M1: ?";
            stats.SetBounds(12,523,630,21);stats.ForeColor=muted;Controls.Add(stats);
            urlBox.SetBounds(12,550,522,25);urlBox.Text=cfg.Url;Controls.Add(urlBox);
            Button apply=ButtonAt("Use URL",541,548,101);apply.Click+=delegate{UseUrl();};
            Button refresh=ButtonAt("Refresh",12,583,75);refresh.Click+=delegate{if(mode!="LIVE"){mode="LIVE";ResetStream();}paused=false;pause.Text="Pause";nextPoll=DateTime.UtcNow;StartFetch();};
            pause.SetBounds(92,583,68,27);pause.Text="Pause";pause.BackColor=Color.FromArgb(222,229,237);pause.ForeColor=Color.Black;pause.UseVisualStyleBackColor=false;Controls.Add(pause);
            pause.Click+=delegate{paused=!paused;pause.Text=paused?"Resume":"Pause";if(!paused){ClearCheckConfirmation();if(mode!="LIVE")ResetStream();mode="LIVE";nextPoll=DateTime.UtcNow;StartFetch();}};
            sound.SetBounds(171,586,73,23);sound.Text="Sound";sound.Checked=cfg.Sound;Controls.Add(sound);
            sound.CheckedChanged+=delegate{cfg.Sound=sound.Checked;SaveSettings();};
            top.SetBounds(248,586,66,23);top.Text="Top";top.Checked=cfg.AlwaysOnTop;TopMost=top.Checked;Controls.Add(top);
            top.CheckedChanged+=delegate{cfg.AlwaysOnTop=top.Checked;TopMost=top.Checked;SaveSettings();};
            Button demoOff=ButtonAt("Test OFF",321,583,86);demoOff.Click+=delegate{Demo("off.png");};
            Button demoOn=ButtonAt("Test ON",413,583,86);demoOn.Click+=delegate{Demo("on.png");};
            Button baseline=ButtonAt("Set OFF view",505,583,137);baseline.Click+=delegate{SetOffView();};
            tips.SetToolTip(baseline,"Only use while the camera faces west and signals are visibly OFF. Saves the current view as the scene reference.");
            history.SetBounds(12,619,630,74);history.BackColor=Color.FromArgb(10,15,20);history.ForeColor=muted;history.BorderStyle=BorderStyle.FixedSingle;history.HorizontalScrollbar=true;Controls.Add(history);
            count.SetBounds(12,699,630,20);count.ForeColor=muted;Controls.Add(count);
            tips.SetToolTip(picture,"Use Show boxes / Hide boxes to toggle all debugging outlines and labels. M2 signal (left) and M1 signal (right) use separate labels. Solid boxes mark the exact aligned color samples. Dashed boxes with ? show unconfirmed candidate positions and are not sampled. The larger outline shows signal search and the dotted box shows ON/OFF measurement. Train headings use the wider picture.");
            InitializeBoxEditor(imageMenu);
            fetch.WorkerSupportsCancellation=true;fetch.DoWork+=Download;fetch.RunWorkerCompleted+=Downloaded;
            clock.Interval=500;clock.Tick+=Tick;
            Shown+=delegate{clock.Start();StartFetch();};
            FormClosing+=HandleClosing;
            if(startupNote.Length>0)AddEvent("STARTUP NOTE",startupNote);
            DiagnosticNote("STARTUP",DiagnosticInformation()+"Startup note: "+startupNote);
        }
        static string ChooseDataPath(string folder) {
            string[] choices={Path.Combine(folder,"data"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LaPlataWestSignalWatch")};
            foreach(string p in choices)try{Directory.CreateDirectory(p);string test=Path.Combine(p,"write-"+Guid.NewGuid().ToString("N")+".tmp");File.WriteAllText(test,"");File.Delete(test);return p;}catch{}
            throw new IOException("Cannot create a writable settings folder. Extract the ZIP into a normal user folder.");
        }
        static void CommitFile(string temp,string target) {
            if(!File.Exists(target)){File.Move(temp,target);return;}
            try {File.Replace(temp,target,target+".bak");}
            catch(PlatformNotSupportedException){File.Copy(temp,target,true);File.Delete(temp);}
        }
        void HandleClosing(object sender,FormClosingEventArgs e) {
            closing=true;clock.Stop();CloseToast();cfg.WindowX=Left;cfg.WindowY=Top;SaveSettings();
            if(fetch.IsBusy) {
                e.Cancel=true;Hide();fetch.CancelAsync();
                lock(requestLock)if(activeRequest!=null)try{activeRequest.Abort();}catch{}
            }
        }
        Label LabelAt(string text,int x,int y,int w,int h,int size) {
            Label l=new Label();l.SetBounds(x,y,w,h);l.Text=text;l.Font=new Font("Segoe UI",size);Controls.Add(l);return l;
        }
        Button ButtonAt(string text,int x,int y,int w) {
            Button b=new Button();b.SetBounds(x,y,w,27);b.Text=text;b.ForeColor=Color.Black;b.BackColor=Color.FromArgb(222,229,237);b.UseVisualStyleBackColor=false;Controls.Add(b);return b;
        }
        Settings LoadSettings() {
            try {using(FileStream f=File.OpenRead(settingsPath)) {
                Settings s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);
                s.Normalize();
                return s;
            }}catch{return new Settings();}
        }
        void SaveSettings() {
            try {string tmp=settingsPath+".tmp";using(FileStream f=File.Create(tmp)) new XmlSerializer(typeof(Settings)).Serialize(f,cfg);
                CommitFile(tmp,settingsPath);
            }catch(Exception e){detail="Could not save settings: "+e.Message;}
        }
        void ResetStream() {
            DiagnosticNote("STREAM RESET","Previous mode="+mode+"; state="+currentState+"; pending="+checkImageCount+". Next image establishes a silent baseline.");
            detector.ResetTracking();
            ClearCheckConfirmation();
            lastSignalState="";CloseToast();
            streamGeneration++;nextPoll=DateTime.UtcNow;
            currentState="";lastHash="";previousCrop=null;reading=null;imageModifiedUtc=null;sceneLatched=false;
            frameId="";frameHash="";
            sameSince=DateTime.MinValue;changedUtc=DateTime.MinValue;lastPollUtc=DateTime.MinValue;flashUntil=DateTime.MinValue;
        }
        void UseUrl() {
            if(fetch.IsBusy) {MessageBox.Show("Wait for the current image request to finish.");return;}
            Uri u;if(!Uri.TryCreate(urlBox.Text.Trim(),UriKind.Absolute,out u)||(u.Scheme!="http"&&u.Scheme!="https")) {MessageBox.Show("Enter a direct HTTP or HTTPS image URL.");return;}
            cfg.Url=u.AbsoluteUri;SaveSettings();mode="LIVE";paused=false;pause.Text="Pause";ResetStream();StartFetch();
        }
        void StartFetch() {
            if(closing||paused||editingBoxes||mode!="LIVE"||fetch.IsBusy)return;
            nextPoll=DateTime.UtcNow.AddSeconds(cfg.PollSeconds);fetchGeneration=streamGeneration;fetch.RunWorkerAsync(cfg.Url);
        }
        void Download(object sender,DoWorkEventArgs e) {
            if(closing||fetch.CancellationPending){e.Cancel=true;return;}
            try {ServicePointManager.SecurityProtocol=(SecurityProtocolType)((int)ServicePointManager.SecurityProtocol|3072);}catch{}
            string url=(string)e.Argument;
            HttpWebRequest q=(HttpWebRequest)WebRequest.Create(url);
            q.UserAgent="LaPlataWestSignalWatch/0.20";q.Timeout=12000;q.ReadWriteTimeout=12000;
            q.CachePolicy=new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            q.Headers["Cache-Control"]="no-cache";q.Headers["Pragma"]="no-cache";
            FetchResult r=new FetchResult();
            lock(requestLock)activeRequest=q;
            try {
            if(closing||fetch.CancellationPending){e.Cancel=true;return;}
            using(HttpWebResponse p=(HttpWebResponse)q.GetResponse()) {
                DateTime modified;
                if(DateTime.TryParse(p.Headers["Last-Modified"],CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out modified))r.ModifiedUtc=modified.ToUniversalTime();
                if(p.ContentLength>8*1024*1024)throw new Exception("Snapshot exceeds 8 MB.");
                using(Stream s=p.GetResponseStream()) using(MemoryStream m=new MemoryStream()) {
                    byte[] buf=new byte[16384];int n;
                    DateTime deadline=DateTime.UtcNow.AddSeconds(15);
                    while((n=s.Read(buf,0,buf.Length))>0) {m.Write(buf,0,n);if(m.Length>8*1024*1024||DateTime.UtcNow>deadline)throw new Exception("Snapshot too large or request timed out.");}
                    byte[] bytes=m.ToArray();r.SourceBytes=bytes;using(SHA256 sha=SHA256.Create())r.Hash=Convert.ToBase64String(sha.ComputeHash(bytes));
                    m.Position=0;using(Image img=Image.FromStream(m)) {
                        if(img.Width<160||img.Height<90||((long)img.Width*img.Height)>20000000) throw new Exception("Unexpected snapshot dimensions.");
                        r.Image=new Bitmap(img);
                    }
                }
            }
            if(closing||fetch.CancellationPending){if(r.Image!=null)r.Image.Dispose();e.Cancel=true;return;}
            r.DownloadedUtc=DateTime.UtcNow;e.Result=r;
            } catch {if(r.Image!=null)r.Image.Dispose();if(closing||fetch.CancellationPending){e.Cancel=true;return;}throw;}
            finally {lock(requestLock)activeRequest=null;}
        }
        void Downloaded(object sender,RunWorkerCompletedEventArgs e) {
            FetchResult f=e.Error==null&&!e.Cancelled?(FetchResult)e.Result:null;
            if(closing||IsDisposed) {if(f!=null)f.Image.Dispose();if(!IsDisposed)Close();return;}
            if(editingBoxes){if(f!=null)f.Image.Dispose();DiagnosticNote("DISCARDED","Image held while boxes are being moved.");return;}
            if(fetchGeneration!=streamGeneration){if(f!=null)f.Image.Dispose();nextPoll=DateTime.UtcNow;DiagnosticNote("DISCARDED","Response from an earlier stream generation ignored.");return;}
            if(e.Cancelled){DiagnosticNote("DISCARDED","Cancelled download ignored.");return;}
            if(e.Error!=null) {if(mode=="LIVE"&&!paused){lastPollUtc=DateTime.UtcNow;BeginDecision(false);rawState="download_error";SetState("CONNECTION ERROR",e.Error.Message);WritePoll(null);WriteDecision("DOWNLOAD",null);}return;}
            if(mode!="LIVE"||paused) {f.Image.Dispose();DiagnosticNote("DISCARDED","Completed download ignored while paused or in TEST mode.");return;}
            lastPollUtc=f.DownloadedUtc;imageModifiedUtc=f.ModifiedUtc;
            bool duplicate=lastHash==f.Hash;
            if(!duplicate) {sameSince=DateTime.UtcNow;lastHash=f.Hash;}
            SaveDiagnosticFrame(f.SourceBytes,f.Hash);
            ShowImage(f.Image);
            try {ProcessImage(!duplicate);}catch(Exception ex){
                previousCrop=null;string message="Image analysis failed: "+ex.Message;
                if(ConfirmCheckImage(message,!duplicate))SetState("CHECK IMAGE",message);
                rawState="analysis_error";UpdateText();WritePoll(null);WriteDecision("ANALYSIS ERROR",null);
            }
        }
        bool IsStale() {
            DateTime now=DateTime.UtcNow;
            return (imageModifiedUtc.HasValue&&(now-imageModifiedUtc.Value).TotalSeconds>cfg.StaleSeconds)||
                   (sameSince!=DateTime.MinValue&&(now-sameSince).TotalSeconds>cfg.StaleSeconds);
        }
        void ShowImage(Bitmap b) {Bitmap old=currentImage;currentImage=b;picture.Image=b;if(old!=null)old.Dispose();}
        void ProcessImage() {ProcessImage(true);}
        void ProcessImage(bool freshPixels) {
            BeginDecision(freshPixels);
            reading=AnalyzeDetectionFrame();
            ApplyTrainDisplay();
            ApplySignalColors();
            rawState=reading.State;
            if(!freshPixels)reading.Changed=false;
            if(mode=="LIVE"&&reading.State!="DAYLIGHT / DISARMED"&&IsStale()) {
                reading.State="STALE IMAGE";reading.Reason="Snapshot has stopped updating or server file is too old.";
            }
            bool valid=reading.State=="SIGNALS ON"||reading.State=="SIGNALS OFF";
            bool sameState=currentState==reading.State;
            bool confirmed=reading.State!="CHECK IMAGE"||ConfirmCheckImage(reading.Reason,freshPixels);
            if(confirmed) {
                SetState(reading.State,reading.Reason);
                if(valid && reading.Changed && sameState && !sceneLatched) {
                    AddEvent("AREA CHANGED","Watch area changed while signal classification stayed the same.");Alert("AREA CHANGED");sceneLatched=true;
                }else if(valid&&!reading.Changed)sceneLatched=false;
            }else {sceneLatched=false;UpdateText();}
            if(valid)previousCrop=reading.Crop;else previousCrop=null;
            stats.Text=String.Format(CultureInfo.InvariantCulture,"Red: {0} px ({1:P2})   View: {2:F2}/{3:F2}   Change: {4:P0}   Locate: {5}",reading.RedPixels,reading.RedFraction,reading.SceneCorrelation,cfg.SceneMinimum,reading.ChangeFraction,reading.LocationFound?reading.LocationMethod:"searching");
            picture.Invalidate();WritePoll(reading);WriteDecision("IMAGE",reading);
        }
        void ApplyTrainDisplay() {
            if(trainDisplay==null)return;
            try {trainDisplay.Apply(currentImage,reading);}
            catch(Exception ex){DiagnosticNote("TRAIN DISPLAY ERROR",ex.Message);}
        }
        void ApplySignalColors() {
            if(signalColors==null||reading==null||reading.State=="DAYLIGHT / DISARMED"||reading.Gate=="bright_obstruction"||reading.TrainDisplayLabel=="M1 TRAIN"||reading.TrainDisplayLabel=="M2 TRAIN")return;
            try {reading.SignalColors=signalColors.Analyze(currentImage,frameBoxAlignment);}
            catch(Exception ex){DiagnosticNote("SIGNAL COLOR DISPLAY ERROR",ex.Message);}
        }
        void ClearCheckConfirmation() {checkImageCount=0;checkImagePending=false;pendingCheckReason="";}
        bool ConfirmCheckImage(string message,bool freshPixels) {
            if(currentState=="CHECK IMAGE")return true;
            // Re-downloading the same snapshot is not another observation.
            if(freshPixels)checkImageCount=Math.Min(CheckImageFrames,checkImageCount+1);
            checkImagePending=checkImageCount<CheckImageFrames;pendingCheckReason=message;
            if(checkImagePending) {statusColor=Color.FromArgb(245,195,89);alertDecision="suppressed: CHECK IMAGE pending "+checkImageCount+"/"+CheckImageFrames;toastDecision=alertDecision+(freshPixels?" (fresh frame)":" (duplicate does not advance)");}
            return !checkImagePending;
        }
        string PendingCheckMessage() {
            return "Confirming view ("+checkImageCount+"/"+CheckImageFrames+"). Last confirmed: "+(lastSignalState.Length>0?lastSignalState:"none")+".\n"+pendingCheckReason;
        }
        void Demo(string file) {
            try {
            bool entering=mode!="TEST";
            mode="TEST";paused=true;pause.Text="Resume";
            if(entering||currentState=="CONNECTION ERROR"||currentState=="STALE IMAGE")ResetStream();
            imageModifiedUtc=null;lastPollUtc=DateTime.UtcNow;
            byte[] bytes=File.ReadAllBytes(Path.Combine(root,"samples/"+file));string hash;using(SHA256 sha=SHA256.Create())hash=Convert.ToBase64String(sha.ComputeHash(bytes));
            SaveDiagnosticFrame(bytes,hash);
            using(Bitmap original=new Bitmap(Path.Combine(root,"samples/"+file)))ShowImage(new Bitmap(original));
            ProcessImage();
            }catch(Exception e){BeginDecision(false);rawState="test_load_error";previousCrop=null;SetState("CHECK IMAGE","Cannot load test sample: "+e.Message);WriteDecision("TEST ERROR",null);}
        }
        void SetOffView() {
            if(fetch.IsBusy||currentImage==null) {MessageBox.Show("Wait for a complete image first.");return;}
            if(mode!="LIVE") {MessageBox.Show("Resume the live feed, then set its OFF view.");return;}
            if(reading==null||!reading.LocationFound||reading.State!="SIGNALS OFF"||currentState!="SIGNALS OFF"||IsStale()) {MessageBox.Show("Use a located, current nighttime picture with signals confirmed OFF before setting the OFF view.");return;}
            if(MessageBox.Show("Use this as the west-facing night reference? Confirm the signals are OFF in the picture.","Set OFF view",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            try {
                Detector candidate=detector.WithReference(currentImage);
                string path=Path.Combine(dataPath,"reference-off.png"),temp=path+".tmp";
                try {currentImage.Save(temp,ImageFormat.Png);CommitFile(temp,path);}catch{candidate.Dispose();throw;}
                Detector old=detector;detector=candidate;old.Dispose();
                savedOffActive=true;curveReferenceActive=false;
                previousCrop=null;currentState="";sceneLatched=false;ProcessImage();AddEvent("REFERENCE SAVED","Current picture is now the OFF scene reference.");
            }
            catch(Exception e){MessageBox.Show(e.Message);}
        }
        void SetState(string state,string message) {
            if(state!="CHECK IMAGE")ClearCheckConfirmation();
            else checkImagePending=false;
            string previous=currentState;
            bool transition=currentState.Length>0 && state!=currentState;
            bool first=currentState.Length==0;
            if(first||transition) {changedUtc=DateTime.UtcNow;AddEvent(state,message);}
            currentState=state;detail=message;
            statusColor=state=="SIGNALS ON"?Color.FromArgb(255,113,94):state=="SIGNALS OFF"?Color.FromArgb(118,210,158):Color.FromArgb(245,195,89);
            if((transition||(first&&state=="CHECK IMAGE"&&checkImageCount>=CheckImageFrames))&&state!="DAYLIGHT / DISARMED")Alert("confirmed status: "+(first?"startup CHECK IMAGE":previous+" -> "+state));
            else {alertDecision="suppressed: "+(first?"startup baseline":transition?"daylight is silent":"unchanged confirmed state");soundDecision="not requested";}
            UpdateText();
            if(transition&&cfg.Toast) {
                try {ShowToast(previous,state);toastDecision="shown: "+previous+" -> "+state;}
                catch(Exception ex){toastDecision="failed: "+ex.Message;DiagnosticNote("TOAST ERROR",toastDecision);}
            }else toastDecision="suppressed: "+(!cfg.Toast?"Toast unchecked":first?"startup baseline":"unchanged confirmed state");
            if(state=="SIGNALS ON"||state=="SIGNALS OFF") {
                lastSignalState=state;
            }else {
                if(state=="DAYLIGHT / DISARMED")lastSignalState="";
            }
        }
        void CloseToast() {if(notification!=null){SignalToast old=notification;notification=null;if(!old.IsDisposed)old.Close();}}
        void ShowToast(string previous,string state) {
            CloseToast();
            int edge;Rectangle workArea=ToastPlacement.WorkArea(out edge);
            SignalToast popup=new SignalToast(previous,state,Stamp(changedUtc),mode=="TEST",workArea,edge,delegate{
                if(closing||IsDisposed)return;Show();if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Activate();
            });
            notification=popup;popup.FormClosed+=delegate{if(notification==popup)notification=null;};popup.Show();
        }
        void Alert(string why) {
            alertDecision="blink requested: "+why;
            flashUntil=DateTime.UtcNow.AddSeconds(cfg.FlashSeconds);
            if(cfg.Sound) {try{if(player!=null){player.Play();soundDecision="custom WAV playback requested";}else {SystemSounds.Exclamation.Play();soundDecision="system sound playback requested";}}catch(Exception ex){try{SystemSounds.Exclamation.Play();soundDecision="fallback system sound requested after "+ex.Message;}catch(Exception fallback){soundDecision="failed: "+fallback.Message;}}}
            else soundDecision="suppressed: Sound unchecked";
        }
        void Tick(object sender,EventArgs e) {
            if(!editingBoxes&&!paused&&mode=="LIVE") {
                if(!fetch.IsBusy&&DateTime.UtcNow>=nextPoll)StartFetch();
                if(currentState.Length>0&&currentState!="CONNECTION ERROR"&&currentState!="DAYLIGHT / DISARMED"&&IsStale()&&currentState!="STALE IMAGE") {BeginDecision(false);rawState="age_timer";SetState("STALE IMAGE","Snapshot has stopped updating or server file is too old.");WriteDecision("STALE TIMER",null);}
            }
            bool lit=DateTime.UtcNow<flashUntil && ((int)(DateTime.UtcNow.Ticks/TimeSpan.TicksPerMillisecond/500)%2==0);
            status.BackColor=lit?Color.FromArgb(124,44,37):background;status.ForeColor=lit?Color.White:statusColor;
            UpdateText();
        }
        string Stamp(DateTime utc) {
            if(utc==DateTime.MinValue)return "--";
            string local=utc.ToLocalTime().ToString("HH:mm:ss",CultureInfo.InvariantCulture);
            if(central==null)return local+" local";
            return local+" local / "+TimeZoneInfo.ConvertTimeFromUtc(utc,central).ToString("HH:mm:ss",CultureInfo.InvariantCulture)+" LAP";
        }
        void UpdateText() {
            UpdateSignalColorBar();
            string displayedState=DashboardLabel();
            status.Text=(mode=="TEST"?"TEST: ":"")+displayedState;
            reason.Text=checkImagePending?PendingCheckMessage():detail;
            string updated=mode=="TEST"?"saved test sample":imageModifiedUtc.HasValue?Stamp(imageModifiedUtc.Value):"not supplied by server";
            times.Text="Image file updated: "+updated+"\nPolled: "+Stamp(lastPollUtc)+"\nLast change: "+Stamp(changedUtc);
            string age=imageModifiedUtc.HasValue?Math.Max(0,(DateTime.UtcNow-imageModifiedUtc.Value).TotalSeconds).ToString("0",CultureInfo.InvariantCulture)+"s (server)":"unknown (no server date)";
            string next=paused?(mode=="TEST"?"TEST MODE":"PAUSED"):fetch.IsBusy?"Fetching image...":"Next poll in "+Math.Max(0,(int)(nextPoll-DateTime.UtcNow).TotalSeconds)+"s";
            count.Text=next+"   |   Image age: "+age+"   |   Source: ~"+SourceUpdateSeconds+"s   |   Poll: "+cfg.PollSeconds+"s"+(diagnosticError.Length>0?"   |   LOG ERROR":"");
            if(diagnosticError.Length>0)tips.SetToolTip(exportLog,"Log error: "+diagnosticError);
            if(editingBoxes) {
                status.Text="MOVE BOXES";status.ForeColor=Color.FromArgb(245,195,89);
                reason.Text="Drag inside M1/M2 or along Search/Measure borders. Done moving resumes monitoring. Right-click for Reset box positions.";
                count.Text="BOX EDIT MODE   |   Current image held   |   Changes save when you release the mouse";
            }
        }
        SignalColorReading CurrentSignalColors() {
            // A fresh uncertain reading can follow a stale/error state while
            // the four-image state confirmation is still pending. Its current
            // independently located colors are usable once freshness returns.
            if(editingBoxes&&reading!=null)return reading.SignalColors;
            bool blocked=reading==null||(reading.State=="STALE IMAGE"||reading.State=="DAYLIGHT / DISARMED")||
                (!checkImagePending&&(currentState=="STALE IMAGE"||currentState=="CONNECTION ERROR"||currentState=="DAYLIGHT / DISARMED"))||
                (mode=="LIVE"&&IsStale());
            return blocked||reading==null?null:reading.SignalColors;
        }
        void UpdateSignalColorBar() {
            SignalColorReading colors=CurrentSignalColors();
            SetColorLabel(colorM2,"M2",colors==null?null:colors.M2,colors);
            SetColorLabel(colorM1,"M1",colors==null?null:colors.M1,colors);
        }
        void SetColorLabel(Label label,string track,LampEstimate estimate,SignalColorReading colors) {
            string low=estimate==null?"?":estimate.ColorName;
            label.Text=track+": "+low;
            label.ForeColor=low=="GREEN"?Color.FromArgb(113,224,164):low=="RED"?Color.FromArgb(255,127,111):low=="YELLOW"?Color.FromArgb(245,195,89):muted;
            string note="Experimental reflected-color estimate. M2 = left track; M1 = right track. Upper/lower head order is not inferred. '?' means insufficient or mixed evidence. Colors never trigger sound, blink or toast. ";
            if(colors!=null) {
                SceneLocation fit=colors.Alignment==null?null:colors.Alignment.Fit;
                note+=String.Format(CultureInfo.InvariantCulture,"Color fit {0:F3}/{1:F2}; landmarks {2}/2; patch minimum {3:F3}; {4}. ",colors.SceneScore,SignalColorDisplay.AlignmentMinimum,fit==null?0:fit.Landmarks,fit==null?0:fit.PatchMinimum,fit==null?"unavailable":fit.Method);
                note+=colors.Decision+". "+estimate.Evidence();
            }
            else note+="No current usable color reading.";
            if(tips.GetToolTip(label)!=note)tips.SetToolTip(label,note);
        }
        string DashboardLabel() {
            if(reading!=null&&reading.TrainDisplayLabel.Length>0&&
                ((reading.State=="CHECK IMAGE"&&(checkImagePending||currentState=="CHECK IMAGE"))||
                 ((reading.State=="SIGNALS ON"||reading.State=="SIGNALS OFF")&&currentState==reading.State)))return reading.TrainDisplayLabel;
            if(reading!=null&&reading.State=="CHECK IMAGE"&&!reading.LocationFound&&
                (reading.Gate=="scene_mismatch"||reading.Gate=="scene_ambiguous"||reading.Gate=="outside_view"||reading.Gate=="manual_box_alignment")&&
                (checkImagePending||currentState=="CHECK IMAGE"))return "PTZ OUT OF RANGE";
            return checkImagePending?PendingCheckState():currentState.Length==0?"CONNECTING...":currentState;
        }
        void AddEvent(string state,string message) {
            string line=Stamp(DateTime.UtcNow)+"  "+state+"  "+message;history.Items.Insert(0,line);
            while(history.Items.Count>100)history.Items.RemoveAt(history.Items.Count-1);
            try {File.AppendAllText(Path.Combine(dataPath,"events.log"),DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)+" ["+mode+"] "+line+Environment.NewLine);}catch{}
        }
        void BeginDecision(bool fresh) {
            decisionBefore=currentState;decisionCountBefore=checkImageCount;decisionFresh=fresh;
            rawState="";alertDecision="none";soundDecision="not requested";toastDecision="not requested";
        }
        void DiagnosticNote(string origin,string body) {
            if(diagnostics==null)return;
            try {diagnostics.Write(origin,body);}
            catch(Exception ex){diagnosticError=ex.Message;}
        }
        void SaveDiagnosticFrame(byte[] bytes,string hash) {
            if(diagnostics==null){frameId="";frameHash="";return;}
            if(frameHash==hash&&frameId.Length>0&&File.Exists(Path.Combine(diagnostics.Folder,frameId.Replace('/',Path.DirectorySeparatorChar))))return;
            try {frameId=diagnostics.SaveFrame(bytes);frameHash=hash;}
            catch(Exception ex){frameId="";frameHash="";diagnosticError=ex.Message;}
        }
        static string RectText(RectangleF r) {
            return String.Format(CultureInfo.InvariantCulture,"x={0:F5}, y={1:F5}, w={2:F5}, h={3:F5}",r.X,r.Y,r.Width,r.Height);
        }
        static string Counts(int[] a) {return a[0]+", "+a[1]+", "+a[2]+", "+a[3];}
        void WriteDecision(string origin,Reading r) {
            StringBuilder b=new StringBuilder();
            b.AppendLine("mode="+mode+"; fresh_pixels="+decisionFresh+"; source_frame="+(frameId.Length>0?frameId:"unavailable"));
            b.AppendLine("source_sha256_base64="+frameHash+"; source_dimensions="+(currentImage==null?"none":currentImage.Width+"x"+currentImage.Height));
            b.AppendLine("polled_utc="+lastPollUtc.ToString("o",CultureInfo.InvariantCulture)+"; server_modified_utc="+(imageModifiedUtc.HasValue?imageModifiedUtc.Value.ToString("o",CultureInfo.InvariantCulture):"not supplied"));
            b.AppendLine("same_bytes_since_utc="+sameSince.ToString("o",CultureInfo.InvariantCulture)+"; stale="+IsStale()+"; stale_limit_seconds="+cfg.StaleSeconds);
            b.AppendLine("raw_state="+rawState+"; effective_image_state="+(r==null?"none":r.State)+"; gate="+(r==null?origin:r.Gate));
            b.AppendLine("confirmed_before="+decisionBefore+"; confirmed_after="+currentState+"; last_signal="+lastSignalState);
            b.AppendLine("dashboard_label="+DashboardLabel()+"; display labels do not change state or toast");
            b.AppendLine("check_count_before="+decisionCountBefore+"; check_count_after="+checkImageCount+"; required="+CheckImageFrames+"; pending="+checkImagePending);
            b.AppendLine("reason="+(checkImagePending?pendingCheckReason:detail));
            if(r!=null) {
                b.AppendLine(String.Format(CultureInfo.InvariantCulture,"sky={0:F3} (day > {1:F3}); scene={2:F5} (minimum {3:F5}); reference_index={4}; shift_thumbnail={5},{6}",r.Sky,cfg.DaySkyLimit,r.SceneCorrelation,cfg.SceneMinimum,r.ViewIndex,r.ShiftX,r.ShiftY));
                b.AppendLine(String.Format(CultureInfo.InvariantCulture,"location_found={0}; location_method={1}; scene_scale={2:F4}; matched_landmarks={3}; minimum_patch_score={4:F4}",r.LocationFound,r.LocationMethod,r.SceneScale,r.LandmarkMatches,r.LandmarkMinimum));
                b.AppendLine("analysis_utc="+r.AnalysisUtc.ToString("o",CultureInfo.InvariantCulture)+"; La_Plata_sun_elevation="+r.ClockSunElevation.ToString("F3",CultureInfo.InvariantCulture)+"; daylight_time_allowed="+r.DaytimeAllowed+"; daylight_min_elevation="+SolarClock.DaylightMinimumElevation);
                b.AppendLine("analysis_region="+RectText(r.Region)+"; sample_dimensions="+r.SampleWidth+"x"+r.SampleHeight);
                b.AppendLine("search_boxes_shown="+cfg.ShowSearchBoxes+"; search_overlay_region="+RectText(VisibleSignalRegion())+"; signal_watch_region="+RectText(r.WatchRegion)+"; color_watch_region="+RectText(r.SignalColors==null?RectangleF.Empty:r.SignalColors.WatchRegion)+"; search_region="+RectText(r.SearchRegion));
                b.AppendLine(String.Format(CultureInfo.InvariantCulture,"red_pixels={0}; red_fraction={1:F6}; on_min={2:F6}; off_max={3:F6}; red_minimum={4}; other_color_pixels={5} (OFF requires <4)",r.RedPixels,r.RedFraction,cfg.OnFraction,cfg.OffFraction,cfg.RedMinimum,r.OtherColor));
                b.AppendLine(String.Format(CultureInfo.InvariantCulture,"search_red_pixels={0}; minimum_pattern_pixels={1}; reflection_found={2}; ambiguous={3}; bright_pixels={4}; bright_limit={5:F2}",r.SearchRed,r.MinimumPatternPixels,r.ReflectionFound,r.Ambiguous,r.BrightPixels,r.BrightLimit));
                b.AppendLine(String.Format(CultureInfo.InvariantCulture,"area_change={0:F5}; change_limit=0.06; changed={1}; area_alert_latched={2}",r.ChangeFraction,r.Changed,sceneLatched));
                b.AppendLine("watch_quadrants=top-left, top-right, bottom-left, bottom-right; red="+Counts(r.WatchRed)+"; yellow="+Counts(r.WatchYellow));
                b.AppendLine(String.Format(CultureInfo.InvariantCulture,"train_display_cue={0}; white_rail_pixels={1}; white_rail_region={2}; curve_white_pixels={3}",r.TrainDisplayLabel,r.WhiteRailPixels,RectText(r.WhiteRailRegion),r.CurveWhitePixels));
                b.AppendLine(String.Format(CultureInfo.InvariantCulture,"white_decision={0}; white_pattern={1}; white_search_region={2}; white_search_pixels={3}; white_components={4}; white_rejected_shape={5}; white_rejected_boundary={6}; white_rejected_residual={7}; white_rejected_location={8}; white_fill={9:F5}; white_residual={10:F5}; white_slope={11:F5}",r.WhiteDecision,r.WhitePattern,RectText(r.WhiteSearchRegion),r.WhiteSearchPixels,r.WhiteComponents,r.WhiteRejectedShape,r.WhiteRejectedBoundary,r.WhiteRejectedResidual,r.WhiteRejectedLocation,r.WhiteRailFill,r.WhiteRailResidual,r.WhiteRailSlope));
                b.AppendLine(String.Format(CultureInfo.InvariantCulture,"train_scope=whole_picture; train_scene_found={0}; train_scene_score={1:F5}; train_body_pixels={2}; train_span={3},{4}; train_m1_score={5:F5}; train_m2_score={6:F5}; train_decision={7}; train_example={8}; train_acceptance_mode={9}; train_winning_scene_score={10:F5}; train_winning_body_pixels={11}",r.TrainSceneFound,r.TrainSceneScore,r.TrainBodyPixels,r.TrainSpanX,r.TrainSpanY,r.TrainM1Score,r.TrainM2Score,r.TrainDecision,r.TrainExample,r.TrainAcceptanceMode,r.TrainMatchSceneScore,r.TrainMatchBodyPixels));
                b.AppendLine("additional_m2_decision="+r.TrainAdditionalDecision+"; additional_m2_similarity="+r.TrainAdditionalScore.ToString("F5",CultureInfo.InvariantCulture)+"; baseline_m1="+r.TrainM1Score.ToString("F5",CultureInfo.InvariantCulture)+"; additional_min=0.92; additional_separation_from_m1_min=0.18");
                b.AppendLine("current_view_train_decision="+r.TrainViewDecision+"; current_view_m1_similarity="+r.TrainViewM1Score.ToString("F5",CultureInfo.InvariantCulture)+"; current_view_m2_similarity="+r.TrainViewM2Score.ToString("F5",CultureInfo.InvariantCulture));
                if(r.SignalColors!=null) {
                    SignalColorReading colors=r.SignalColors;
                    if(colors.Alignment!=null&&colors.Alignment.Fit!=null) {
                        SceneLocation fit=colors.Alignment.Fit;
                        b.AppendLine(String.Format(CultureInfo.InvariantCulture,"color_fit_method={0}; color_fit_scale={1:F4}; color_fit_shift={2:F3},{3:F3}; color_fit_landmarks={4}; color_fit_patch_minimum={5:F5}; color_fit_ambiguous={6}",fit.Method,fit.Scale,fit.Dx,fit.Dy,fit.Landmarks,fit.PatchMinimum,fit.Ambiguous));
                    }
                    b.AppendLine("signal_color_display_only=true; color_location="+colors.Located+"; color_scene_score="+colors.SceneScore.ToString("F5",CultureInfo.InvariantCulture)+"; color_scene_minimum="+SignalColorDisplay.AlignmentMinimum.ToString("F2",CultureInfo.InvariantCulture)+"; color_decision="+colors.Decision);
                    b.AppendLine("M2_color="+colors.M2.Evidence());
                    b.AppendLine("M1_color="+colors.M1.Evidence());
                }
                b.AppendLine("Color bar uses experimental user-labeled positions and reflected colors, not verified physical head detections. TEST annotations can add yellow pixels.");
            }
            b.AppendLine("alert="+alertDecision+"; sound="+soundDecision+"; toast="+toastDecision);
            b.AppendLine("Sound requested does not prove audible playback. Sound_enabled="+cfg.Sound+"; Toast_enabled="+cfg.Toast);
            DiagnosticNote(origin,b.ToString());
        }
        string DiagnosticInformation() {
            return "La Plata West Signal Watch v0.31\r\nBuild="+typeof(WatchForm).Assembly.GetName().Version+"; CLR="+Environment.Version+"; OS="+Environment.OSVersion+"\r\n"+
                BoxPositionsText()+
                "Export/startup UTC="+DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)+"\r\n"+
                "Source update interval approximately "+SourceUpdateSeconds+" seconds; polls and fresh-image confirmation use newly downloaded source bytes.\r\n"+
                String.Format(CultureInfo.InvariantCulture,"Poll={0}s; stale={1}s; CHECK IMAGE={2} fresh consecutive frames; scene_min={3:F4}; day_sky_max={4:F2}; on_fraction={5:F6}; off_fraction={6:F6}; red_min={7}\r\n",cfg.PollSeconds,cfg.StaleSeconds,CheckImageFrames,cfg.SceneMinimum,cfg.DaySkyLimit,cfg.OnFraction,cfg.OffFraction,cfg.RedMinimum)+
                "Base region="+RectText(new RectangleF((float)cfg.RoiX,(float)cfg.RoiY,(float)cfg.RoiW,(float)cfg.RoiH))+"\r\n"+
                "Sound="+cfg.Sound+"; Toast="+cfg.Toast+"\r\n\r\n"+
                "Active reference index 0="+(savedOffActive?"saved-off.png":"supplied-off.png")+"; index 1="+(curveReferenceActive?"curve-view.jpg":"not loaded")+"\r\n"+
                "decisions/: UTC decision records, measurements, gates, confirmation counts, and alert/toast reasons. Sessions have unique IDs.\r\n"+
                "frames/: exact downloaded source bytes, linked by source_frame and SHA256 in decisions. Duplicates reuse a frame. TEST mode is marked.\r\n"+
                "references/: supplied scene references and any saved OFF reference. Reference index 0 is the active OFF reference; index 1 is the supplied curve scene when loaded.\r\n"+
                "Retention: latest 60 unique snapshots, capped at 20 MiB; latest 6 decision logs rotating near 4 MiB each. Older records may reference frames already expired.\r\n"+
                "Every confirmed status transition can toast. Initial baseline and unchanged status do not toast. Pending CHECK IMAGE does not toast; 4 fresh consecutive uncertain images confirm it.\r\n"+
                "WEST CURVE TRAIN, M2 TRAIN and M1 TRAIN are dashboard-only labels. WEST CURVE TRAIN requires a located bright-obstruction gate and new narrow or sparse branched white light intersecting the registered curve signal area. Faint/different color alone cannot produce it. White-shape acceptance/rejection is logged. It does not infer travel direction from a still image. M1 and M2 use the same wider-picture foreground comparison against labeled examples, aligned using separate static background landmarks. Train appearance never depends on the signal ROI, signal lock, or a red signal reflection. Strong matches require similarity >=0.78 and M1/M2 separation >=0.06. A second path permits similarity >=0.70 only with separation >=0.20, winning background score >=0.90 and >=300 foreground pixels. Both paths retain scene and foreground-extent checks. These are similarity thresholds, not recognition probabilities. Signal state, confirmation count and toast text are unchanged.\r\n"+
                "Daylight requires a confirmed scene, a bright registered sky patch, and a solar-clock elevation of at least -3 degrees at La Plata using PC UTC time. Unconfirmed camera views display PTZ OUT OF RANGE and retain CHECK IMAGE confirmation semantics. The printed timestamp is never read.\r\n"+
                "Sound requests cannot prove audible playback. Daylight is silent. No OCR or camera timestamp recognition. Age uses HTTP Last-Modified and duplicate bytes.\r\n"+
                "Background scene matching relocates the search after pan/zoom using at least two matching landmark patches. Show boxes / Hide boxes toggles all debugging outlines and M1/M2 sample labels, including the adjacent actual color samples and search/measurement outlines. Hidden by default; preference is saved. Recognition continues while hidden. Yellow watch box includes nearby upper/lower groups; inner analysis area retains established glare/change gates. Color quadrant counts do not identify aspects, heads, or M1/M2. The separate experimental color bar uses user-labeled M2-left/M1-right zones with independent scene registration. Colors are reflected-color estimates; upper/lower head order is intentionally not inferred. Color readings do not change alert state, confirmation, sound, blink or toast. Polling is fixed at 60 seconds.\r\n"+
                "This ZIP contains camera images and diagnostic details. Review it before sharing. Export stays on your computer.\r\n";
        }
        void BuildDiagnosticExport(string destination) {
            if(diagnostics==null)throw new IOException("Decision logging is unavailable: "+diagnosticError);
            DiagnosticNote("EXPORT","Diagnostic ZIP requested.");
            Dictionary<string,string> refs=new Dictionary<string,string>();
            refs.Add("supplied-off.png",Path.Combine(root,"samples/off.png"));refs.Add("curve-view.jpg",Path.Combine(root,"samples/curve-view.jpg"));refs.Add("saved-off.png",Path.Combine(dataPath,"reference-off.png"));
            foreach(string name in new string[]{"train-m1.png","train-m1-tanks.png","train-m2.png","train-m2-dim.png","train-m2-cars.jpg","signal-colors-m2-green.png","signal-colors-m2-yellow.png","train-m2-reflections.png","ptz-out-of-range.png","signal-colors-obstructed.png","west-curve-false-positive.png","train-m1-reflections.png","train-m2-mixed.png","signal-colors-ptz-red.png","train-m1-ptz-tanks.png","train-m2-ptz-reflections.png","signal-colors-ptz-latest.png"})refs.Add(name,Path.Combine(root,"samples/"+name));
            diagnostics.Export(destination,DiagnosticInformation(),refs);
        }
        void ExportDecisions() {
            using(SaveFileDialog dialog=new SaveFileDialog()) {
                dialog.Title="Export decisions and recent camera images";dialog.Filter="Diagnostic ZIP (*.zip)|*.zip";dialog.DefaultExt="zip";dialog.AddExtension=true;
                dialog.InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);dialog.FileName="LAP-Signal-Diagnostics-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+".zip";
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try {BuildDiagnosticExport(dialog.FileName);MessageBox.Show(this,"Saved diagnostic log and recent images to:\n"+dialog.FileName,"Export complete");}
                catch(Exception ex){diagnosticError=ex.Message;UpdateText();MessageBox.Show(this,"Could not export diagnostics:\n"+ex.Message,"Export error",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            }
        }
        static string Csv(string s) {return "\""+(s??"").Replace("\"","\"\"")+"\"";}
        string PendingCheckState() {return "CHECK IMAGE ("+checkImageCount+"/"+CheckImageFrames+")";}
        void WritePoll(Reading r) {
            try {
                string file=Path.Combine(dataPath,"polls-v2-"+DateTime.Now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+".csv");
                if(!File.Exists(file))File.AppendAllText(file,"utc,mode,state,red_pixels,red_fraction,scene_correlation,area_change,server_modified_utc,reason"+Environment.NewLine);
                File.AppendAllText(file,Csv(DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture))+","+Csv(mode)+","+Csv(checkImagePending?PendingCheckState():currentState)+","+
                    (r==null?",,,":r.RedPixels.ToString(CultureInfo.InvariantCulture)+","+r.RedFraction.ToString("F5",CultureInfo.InvariantCulture)+","+r.SceneCorrelation.ToString("F4",CultureInfo.InvariantCulture)+","+r.ChangeFraction.ToString("F4",CultureInfo.InvariantCulture))+","+
                    Csv(imageModifiedUtc.HasValue?imageModifiedUtc.Value.ToString("o",CultureInfo.InvariantCulture):"")+","+Csv(checkImagePending?PendingCheckMessage():detail)+Environment.NewLine);
            }catch{}
        }
        RectangleF VisibleSignalRegion() {
            if(reading==null)return RectangleF.Empty;
            RectangleF region=reading.WatchRegion.IsEmpty?reading.Region:reading.WatchRegion;
            if(!reading.SearchRegion.IsEmpty)region=RectangleF.Union(region,reading.SearchRegion);
            SignalColorReading colors=CurrentSignalColors();
            if(colors!=null&&colors.Located&&!colors.WatchRegion.IsEmpty)region=RectangleF.Union(region,colors.WatchRegion);
            return RectangleF.Intersect(region,new RectangleF(0,0,1,1));
        }
        void PaintColorSample(Graphics graphics,string track,LampEstimate estimate,bool confirmed,float w,float h,float ox,float oy) {
            RectangleF area=estimate.Region;if(area.IsEmpty)return;
            RectangleF box=new RectangleF(ox+area.X*w,oy+area.Y*h,area.Width*w,area.Height*h);
            if(!box.IntersectsWith(new RectangleF(0,0,picture.ClientSize.Width,picture.ClientSize.Height)))return;
            using(Pen pen=new Pen(Color.LightGoldenrodYellow,2)) {
                if(!confirmed)pen.DashStyle=DashStyle.Dash;
                graphics.DrawRectangle(pen,box.X,box.Y,box.Width,box.Height);
            }
            string text=track+" signal"+(confirmed?"":" ?");
            RectangleF label=SignalBoxLabel(graphics,text,track=="M1",box,true);
            graphics.FillRectangle(Brushes.Black,label);
            graphics.DrawString(text,boxLabelFont,Brushes.LightGoldenrodYellow,label.X+2,label.Y+1);
        }
        void PaintRegion(object sender,PaintEventArgs e) {
            if(currentImage==null||reading==null)return;
            float factor=Math.Min((float)picture.ClientSize.Width/currentImage.Width,(float)picture.ClientSize.Height/currentImage.Height);
            float w=currentImage.Width*factor,h=currentImage.Height*factor,ox=(picture.ClientSize.Width-w)/2,oy=(picture.ClientSize.Height-h)/2;
            RectangleF rr=VisibleSignalRegion();
            SignalColorReading colors=CurrentSignalColors();
            if(editingBoxes){PaintEditableBoxes(e.Graphics);return;}
            if(cfg.ShowSearchBoxes) {
                using(Pen p=new Pen(Color.Yellow,2)) {
                    if(!reading.LocationFound||(colors!=null&&!colors.Located))p.DashStyle=DashStyle.Dash;
                    RectangleF search=reading.LocationFound&&!reading.SearchRegion.IsEmpty?reading.SearchRegion:rr;
                    e.Graphics.DrawRectangle(p,ox+search.X*w,oy+search.Y*h,search.Width*w,search.Height*h);
                }
                if(reading.LocationFound&&!reading.Region.IsEmpty)using(Pen inner=new Pen(Color.Goldenrod,1)) {
                    inner.DashStyle=DashStyle.Dot;
                    RectangleF area=reading.Region;
                    e.Graphics.DrawRectangle(inner,ox+area.X*w,oy+area.Y*h,area.Width*w,area.Height*h);
                }
                // Geometry is useful for troubleshooting even if color sampling
                // is blocked. Never present these as confirmed/current samples.
                SignalColorReading overlay=reading.SignalColors;
                bool confirmed=colors!=null&&colors.Located;
                if(overlay!=null) {
                    PaintColorSample(e.Graphics,"M2",overlay.M2,confirmed,w,h,ox,oy);
                    PaintColorSample(e.Graphics,"M1",overlay.M1,confirmed,w,h,ox,oy);
                }
                if(reading.LocationFound&&!confirmed) {
                    string note=overlay==null?"M1/M2 boxes unavailable: color calibration missing":
                        !overlay.Located?String.Format(CultureInfo.InvariantCulture,"M1/M2 locating: color fit {0:F2}/{1:F2}, landmarks {2}/2; colors ?",overlay.SceneScore,SignalColorDisplay.AlignmentMinimum,overlay.Alignment==null||overlay.Alignment.Fit==null?0:overlay.Alignment.Fit.Landmarks):
                        "M1/M2 reading unavailable: dashed boxes show prior positions";
                    SizeF size=e.Graphics.MeasureString(note,boxLabelFont);
                    float y=Math.Max(0,picture.ClientSize.Height-size.Height-6);
                    e.Graphics.FillRectangle(Brushes.Black,5,y,size.Width+6,size.Height+2);
                    e.Graphics.DrawString(note,boxLabelFont,Brushes.LightGoldenrodYellow,8,y+1);
                }
            }
            if(!reading.LocationFound)using(Brush brush=new SolidBrush(Color.Yellow)) {
                string label="PTZ out of range / waiting for west curve";
                e.Graphics.DrawString(label,Font,brush,8,picture.ClientSize.Height-24);
            }
        }
        protected override void Dispose(bool disposing) {
            if(disposing) {CloseToast();clock.Dispose();boxLabelFont.Dispose();if(picture.ContextMenuStrip!=null)picture.ContextMenuStrip.Dispose();if(currentImage!=null){picture.Image=null;currentImage.Dispose();}if(player!=null)player.Dispose();if(detector!=null)detector.Dispose();tips.Dispose();}
            base.Dispose(disposing);
        }
    }
}
