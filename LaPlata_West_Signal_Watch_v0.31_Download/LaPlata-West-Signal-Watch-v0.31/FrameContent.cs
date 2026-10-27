using System;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;
namespace LapWest {
    // Reference screenshots can contain the PictureBox's black side margins.
    // Normalize those previews to camera content, then map results back for UI.
    sealed class FrameContent : IDisposable {
        public readonly Bitmap Image;
        public readonly RectangleF Bounds;
        public readonly bool HasPreviewMargins;
        static bool BlackColumn(Bitmap image,int x) {
            for(int y=0;y<image.Height;y++){Color c=image.GetPixel(x,y);if(c.R!=0||c.G!=0||c.B!=0)return false;}
            return true;
        }
        public FrameContent(Bitmap source) {
            int left=0,right=source.Width;
            if((double)source.Width/source.Height>16.0/9+.03) {
                while(left<source.Width/5&&BlackColumn(source,left))left++;
                while(right>source.Width*4/5&&BlackColumn(source,right-1))right--;
                if(left<2||source.Width-right<2||Math.Abs((double)(right-left)/source.Height-16.0/9)>.03){left=0;right=source.Width;}
            }
            HasPreviewMargins=left>0;
            Bounds=new RectangleF((float)left/source.Width,0,(float)(right-left)/source.Width,1);
            Image=new Bitmap(right-left,source.Height,PixelFormat.Format24bppRgb);
            using(Graphics graphics=Graphics.FromImage(Image))graphics.DrawImageUnscaled(source,-left,0);
            if(HasPreviewMargins) {
                BitmapData data=Image.LockBits(new Rectangle(0,0,Image.Width,Image.Height),ImageLockMode.ReadWrite,PixelFormat.Format24bppRgb);
                try {
                    int stride=Math.Abs(data.Stride);byte[] bytes=new byte[stride*Image.Height];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
                    for(int y=0;y<Image.Height;y++)for(int x=0;x<Image.Width;x++) {
                        int k=y*stride+x*3;int b=bytes[k],g=bytes[k+1],r=bytes[k+2];
                        // Exact overlay colors from this dashboard, only in
                        // detected padded previews, never normal camera frames.
                        if((r==255&&g==255&&b==0)||(r==218&&g==165&&b==32)||(r==250&&g==250&&b==210)||
                           (r==250&&g==255&&b==8)||(r==213&&g==162&&b==21))bytes[k]=bytes[k+1]=bytes[k+2]=0;
                    }
                    Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
                }finally{Image.UnlockBits(data);}
            }
        }
        public RectangleF ToContent(RectangleF area) {return new RectangleF((area.X-Bounds.X)/Bounds.Width,(area.Y-Bounds.Y)/Bounds.Height,area.Width/Bounds.Width,area.Height/Bounds.Height);}
        public RectangleF ToFrame(RectangleF area) {return new RectangleF(Bounds.X+area.X*Bounds.Width,Bounds.Y+area.Y*Bounds.Height,area.Width*Bounds.Width,area.Height*Bounds.Height);}
        public void Dispose(){Image.Dispose();}
    }
}
