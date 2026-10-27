using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LapWest {
    public sealed partial class WatchForm {
        readonly Button moveBoxes=new Button();
        readonly Font boxLabelFont=new Font("Segoe UI",8,FontStyle.Bold);
        readonly ToolStripMenuItem moveBoxesMenu=new ToolStripMenuItem("Move boxes");
        readonly ToolStripMenuItem resetBoxesMenu=new ToolStripMenuItem("Reset box positions");
        readonly RectangleF[] editableBoxes=new RectangleF[4];
        static readonly string[] BoxNames={"M2 signal","M1 signal","Measure","Search"};
        bool editingBoxes,boxPositionsChanged;
        int draggedBox=-1;
        PointF boxDragStart;
        RectangleF boxDragOriginal,boxDragPreview;
        BoxAlignment editBoxAlignment,frameBoxAlignment;
        Reading beforeBoxEdit;
        void InitializeBoxEditor(ContextMenuStrip menu) {
            times.Width=522;
            moveBoxes.SetBounds(541,134,101,27);moveBoxes.Text="Move boxes";
            moveBoxes.ForeColor=Color.Black;moveBoxes.BackColor=Color.FromArgb(222,229,237);moveBoxes.UseVisualStyleBackColor=false;Controls.Add(moveBoxes);
            moveBoxes.Click+=delegate{SetBoxEditing(!editingBoxes);};
            moveBoxesMenu.Click+=delegate{SetBoxEditing(!editingBoxes);};
            resetBoxesMenu.Click+=delegate{ResetBoxPositions();};
            menu.Items.Add(moveBoxesMenu);menu.Items.Add(resetBoxesMenu);
            tips.SetToolTip(moveBoxes,"Hold the current image and move detection boxes. Drag inside M1/M2 or along Search/Measure borders. Done moving resumes monitoring. Right-click the image to reset positions.");
            picture.MouseDown+=BoxMouseDown;picture.MouseMove+=BoxMouseMove;picture.MouseUp+=BoxMouseUp;
            picture.MouseCaptureChanged+=delegate{if(draggedBox>=0&&!picture.Capture)CancelBoxDrag();};
            KeyPreview=true;
            KeyDown+=delegate(object sender,KeyEventArgs e){if(editingBoxes&&e.KeyCode==Keys.Escape){if(draggedBox>=0)CancelBoxDrag();else SetBoxEditing(false);e.Handled=true;e.SuppressKeyPress=true;}};
        }
        Reading AnalyzeDetectionFrame() {
            frameBoxAlignment=null;
            if(signalColors!=null&&(cfg.SignalSearchBox.Enabled||cfg.SignalMeasureBox.Enabled))frameBoxAlignment=signalColors.Locate(currentImage);
            return detector.Analyze(currentImage,previousCrop,DateTime.UtcNow,frameBoxAlignment);
        }
        void SetBoxEditing(bool enabled) {
            if(enabled==editingBoxes)return;
            if(enabled) {
                if(currentImage==null||signalColors==null){MessageBox.Show(this,"Wait for a camera image and color calibration before moving boxes.","Move boxes");return;}
                BoxAlignment alignment=signalColors.Locate(currentImage);
                if(!alignment.Located){MessageBox.Show(this,"The west-curve background must be recognized before box positions can be saved. Wait for the camera to return to that view.","Move boxes");return;}
                beforeBoxEdit=reading;editBoxAlignment=alignment;boxPositionsChanged=false;editingBoxes=true;
                showSearchBoxes.Checked=true;
                RefreshEditableBoxes();
            } else {
                CancelBoxDrag();editingBoxes=false;
                if(boxPositionsChanged)RecheckBoxPositions();else reading=beforeBoxEdit;
                beforeBoxEdit=null;editBoxAlignment=null;
                if(mode=="LIVE"&&!paused)nextPoll=DateTime.UtcNow;
            }
            moveBoxes.Text=editingBoxes?"Done moving":"Move boxes";moveBoxesMenu.Checked=editingBoxes;
            foreach(Control control in Controls)if(control is Button&&control!=moveBoxes&&control!=boxesToggle&&control!=exportLog)control.Enabled=!editingBoxes;
            urlBox.Enabled=!editingBoxes;picture.Cursor=Cursors.Default;
            UpdateText();picture.Invalidate();
        }
        void RefreshEditableBoxes() {
            editableBoxes[0]=signalColors.SampleArea(false,editBoxAlignment);
            editableBoxes[1]=signalColors.SampleArea(true,editBoxAlignment);
            editableBoxes[2]=reading!=null&&reading.LocationFound?reading.Region:RectangleF.Empty;
            editableBoxes[3]=reading!=null&&reading.LocationFound?reading.SearchRegion:RectangleF.Empty;
        }
        void PreviewBoxPositions() {
            previousCrop=null;
            reading=AnalyzeDetectionFrame();ApplyTrainDisplay();ApplySignalColors();
            RefreshEditableBoxes();UpdateText();picture.Invalidate();
        }
        void RecheckBoxPositions() {
            detector.ResetTracking();previousCrop=null;ClearCheckConfirmation();sceneLatched=false;
            currentState="";lastSignalState="";CloseToast();flashUntil=DateTime.MinValue;
            if(currentImage!=null)ProcessImage(false);
        }
        void ResetBoxPositions() {
            CancelBoxDrag();cfg.M2Box.Reset();cfg.M1Box.Reset();cfg.SignalMeasureBox.Reset();cfg.SignalSearchBox.Reset();
            SaveSettings();DiagnosticNote("BOX POSITIONS","Restored automatic/default sample and search positions.");
            if(editingBoxes){boxPositionsChanged=true;detector.ResetTracking();PreviewBoxPositions();}
            else RecheckBoxPositions();
        }
        DetectionBox Placement(int index) {return index==0?cfg.M2Box:index==1?cfg.M1Box:index==2?cfg.SignalMeasureBox:cfg.SignalSearchBox;}
        bool ImageGeometry(out float w,out float h,out float ox,out float oy) {
            w=h=ox=oy=0;if(currentImage==null)return false;
            float scale=Math.Min((float)picture.ClientSize.Width/currentImage.Width,(float)picture.ClientSize.Height/currentImage.Height);
            w=currentImage.Width*scale;h=currentImage.Height*scale;ox=(picture.ClientSize.Width-w)/2;oy=(picture.ClientSize.Height-h)/2;return w>0&&h>0;
        }
        RectangleF OnPicture(RectangleF area) {
            float w,h,ox,oy;if(!ImageGeometry(out w,out h,out ox,out oy))return RectangleF.Empty;
            return new RectangleF(ox+area.X*w,oy+area.Y*h,area.Width*w,area.Height*h);
        }
        RectangleF BoxLabel(Graphics graphics,int index,RectangleF box) {
            return SignalBoxLabel(graphics,BoxNames[index],index==1,box,index<2);
        }
        RectangleF SignalBoxLabel(Graphics graphics,string text,bool m1,RectangleF box,bool signal) {
            SizeF size=graphics.MeasureString(text,boxLabelFont);size.Width+=4;size.Height+=2;
            // Adjacent samples are narrower than their labels. Put M2 above
            // and to the left, M1 below and to the right, in live and edit views.
            float x=signal&&!m1?box.Right-size.Width:box.Left;
            float y=signal&&m1?box.Bottom+3:box.Top-size.Height-3;
            x=Math.Max(0,Math.Min(picture.ClientSize.Width-size.Width,x));
            y=Math.Max(0,Math.Min(picture.ClientSize.Height-size.Height,y));
            return new RectangleF(x,y,size.Width,size.Height);
        }
        int HitBox(Point point) {
            if(!editingBoxes)return -1;
            float w,h,ox,oy;if(!ImageGeometry(out w,out h,out ox,out oy)||point.X<ox||point.X>ox+w||point.Y<oy||point.Y>oy+h)return -1;
            // Small sample boxes have priority over the larger enclosing boxes.
            for(int i=0;i<2;i++)if(!editableBoxes[i].IsEmpty&&OnPicture(editableBoxes[i]).Contains(point))return i;
            using(Graphics graphics=picture.CreateGraphics())for(int i=0;i<4;i++) {
                if(editableBoxes[i].IsEmpty)continue;
                RectangleF box=OnPicture(editableBoxes[i]);RectangleF border=box; border.Inflate(4,4);
                RectangleF inside=box;inside.Inflate(-4,-4);
                if((border.Contains(point)&&!inside.Contains(point))||BoxLabel(graphics,i,box).Contains(point))return i;
            }
            return -1;
        }
        void BoxMouseDown(object sender,MouseEventArgs e) {
            if(e.Button!=MouseButtons.Left||!editingBoxes)return;
            int hit=HitBox(e.Location);if(hit<0)return;
            float w,h,ox,oy;if(!ImageGeometry(out w,out h,out ox,out oy))return;
            draggedBox=hit;boxDragStart=new PointF((e.X-ox)/w,(e.Y-oy)/h);
            boxDragOriginal=boxDragPreview=editableBoxes[hit];picture.Capture=true;picture.Cursor=Cursors.SizeAll;
        }
        void UpdateBoxDrag(Point point) {
            float w,h,ox,oy;if(draggedBox<0||!ImageGeometry(out w,out h,out ox,out oy))return;
            float x=boxDragOriginal.X+(point.X-ox)/w-boxDragStart.X,y=boxDragOriginal.Y+(point.Y-oy)/h-boxDragStart.Y;
            boxDragPreview=new RectangleF(Math.Max(0,Math.Min(1-boxDragOriginal.Width,x)),Math.Max(0,Math.Min(1-boxDragOriginal.Height,y)),boxDragOriginal.Width,boxDragOriginal.Height);
            picture.Invalidate();
        }
        void BoxMouseMove(object sender,MouseEventArgs e) {
            if(!editingBoxes)return;
            if(draggedBox>=0)UpdateBoxDrag(e.Location);else picture.Cursor=HitBox(e.Location)>=0?Cursors.SizeAll:Cursors.Default;
        }
        void BoxMouseUp(object sender,MouseEventArgs e) {
            if(e.Button!=MouseButtons.Left||draggedBox<0)return;
            UpdateBoxDrag(e.Location);int target=draggedBox;RectangleF area=boxDragPreview,original=boxDragOriginal;CancelBoxDrag();
            if(Math.Abs(area.X-original.X)<.000001&&Math.Abs(area.Y-original.Y)<.000001)return;
            DetectionBox placement=Placement(target);placement.Area=editBoxAlignment.ToReference(area);placement.Enabled=true;placement.Normalize();
            boxPositionsChanged=true;SaveSettings();DiagnosticNote("BOX POSITION",BoxNames[target]+" moved to "+RectText(area)+"; saved reference coordinates="+RectText(placement.Area));
            PreviewBoxPositions();
        }
        void CancelBoxDrag() {draggedBox=-1;picture.Capture=false;picture.Cursor=Cursors.Default;picture.Invalidate();}
        void PaintEditableBoxes(Graphics graphics) {
            // Larger outlines first, so tiny color samples remain selectable.
            for(int i=3;i>=0;i--) {
                RectangleF area=draggedBox==i?boxDragPreview:editableBoxes[i];if(area.IsEmpty)continue;
                RectangleF box=OnPicture(area);Color color=i==3?Color.Yellow:i==2?Color.Goldenrod:Color.LightGoldenrodYellow;
                using(Pen pen=new Pen(color,i<2||draggedBox==i?2:1)){if(i==2)pen.DashStyle=DashStyle.Dot;graphics.DrawRectangle(pen,box.X,box.Y,box.Width,box.Height);}
                RectangleF label=BoxLabel(graphics,i,box);graphics.FillRectangle(Brushes.Black,label);
                using(Brush brush=new SolidBrush(color))graphics.DrawString(BoxNames[i],boxLabelFont,brush,label.Location);
            }
        }
        string BoxPositionsText() {
            return "Manual boxes (color reference coordinates): M2="+(cfg.M2Box.Enabled?RectText(cfg.M2Box.Area):"default")+"; M1="+(cfg.M1Box.Enabled?RectText(cfg.M1Box.Area):"default")+"; Search="+(cfg.SignalSearchBox.Enabled?RectText(cfg.SignalSearchBox.Area):"automatic")+"; Measure="+(cfg.SignalMeasureBox.Enabled?RectText(cfg.SignalMeasureBox.Area):"automatic")+"\r\n";
        }
    }
}
