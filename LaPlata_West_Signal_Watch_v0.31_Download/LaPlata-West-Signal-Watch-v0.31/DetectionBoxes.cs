using System;
using System.Drawing;
using System.Xml.Serialization;

namespace LapWest {
    // A manual rectangle in the color reference's normalized camera content.
    // It is projected by the same background fit on every new image.
    public sealed class DetectionBox {
        public bool Enabled;
        public float X,Y,Width,Height;
        [XmlIgnore] public RectangleF Area {
            get {return new RectangleF(X,Y,Width,Height);}
            set {X=value.X;Y=value.Y;Width=value.Width;Height=value.Height;}
        }
        static bool Finite(float n) {return !Single.IsNaN(n)&&!Single.IsInfinity(n);}
        public void Normalize() {
            if(!Finite(X)||!Finite(Y)||!Finite(Width)||!Finite(Height)||X<-2||X>2||Y<-2||Y>2||Width<.0001||Width>2||Height<.0001||Height>2)Reset();
        }
        public void Reset() {Enabled=false;X=Y=Width=Height=0;}
    }
    public sealed class BoxAlignment {
        public readonly SceneLocation Fit;
        public readonly RectangleF ContentBounds;
        public bool Located {get{return Fit!=null&&Fit.Found;}}
        public BoxAlignment(SceneLocation fit,RectangleF bounds) {Fit=fit;ContentBounds=bounds;}
        public RectangleF ToFrame(RectangleF area) {
            RectangleF p=SceneLocator.Project(area,Fit);
            return new RectangleF(ContentBounds.X+p.X*ContentBounds.Width,ContentBounds.Y+p.Y*ContentBounds.Height,p.Width*ContentBounds.Width,p.Height*ContentBounds.Height);
        }
        public RectangleF ToReference(RectangleF area) {
            RectangleF p=new RectangleF((area.X-ContentBounds.X)/ContentBounds.Width,(area.Y-ContentBounds.Y)/ContentBounds.Height,area.Width/ContentBounds.Width,area.Height/ContentBounds.Height);
            return SceneLocator.Unproject(p,Fit);
        }
    }
}
