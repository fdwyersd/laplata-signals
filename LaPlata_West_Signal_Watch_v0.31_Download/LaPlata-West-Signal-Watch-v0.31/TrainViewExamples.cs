using System;using System.Drawing;using System.IO;using System.Collections.Generic;
namespace LapWest {
    // Optional, isolated fallback for the two user-labeled current-view trains.
    // Original accepted labels and their reference fits are never overwritten.
    sealed class TrainViewExamples {
        sealed class Example {public string Label,Name;public TrainDisplayClassifier.Features Body;}
        readonly SceneLocator locator=new SceneLocator(true);
        readonly List<Example> examples=new List<Example>();
        double[] empty;
        public string Status="not loaded";
        public TrainViewExamples(string root) {
            try {
                using(Bitmap source=new Bitmap(Path.Combine(root,"samples/signal-colors-ptz-red.png")))
                using(FrameContent frame=new FrameContent(source)) {
                    empty=TrainDisplayClassifier.Thumbnail(frame.Image);
                    locator.SetReference(frame.Image,new RectangleF(.585f,.19f,.18f,.10f));
                }
                string[] names={"train-m1-ptz-tanks.png","train-m2-ptz-reflections.png"};
                for(int i=0;i<names.Length;i++)using(Bitmap source=new Bitmap(Path.Combine(root,"samples/"+names[i])))using(FrameContent frame=new FrameContent(source)) {
                    double[] pixels=TrainDisplayClassifier.Thumbnail(frame.Image);locator.Reset();SceneLocation fit=locator.Locate(pixels,.78,true);
                    if(!TrainDisplayClassifier.ValidFit(fit))throw new Exception("Current-view example cannot be aligned: "+names[i]);
                    Example example=new Example();example.Name=names[i];example.Label=i==0?"M1 TRAIN":"M2 TRAIN";
                    example.Body=TrainDisplayClassifier.Foreground(pixels,empty,fit);
                    if(example.Body.Pixels<300||example.Body.SpanX<50||example.Body.SpanY<12)throw new Exception("Insufficient car extent: "+names[i]);
                    examples.Add(example);
                }
                locator.Reset();Status="ready";
            }catch(Exception ex){examples.Clear();Status="current-view examples unavailable: "+ex.Message;}
        }
        public void Apply(Bitmap source,Reading reading) {
            reading.TrainViewDecision=Status;
            if(examples.Count!=2||reading.TrainDisplayLabel.Length>0)return;
            using(FrameContent frame=new FrameContent(source)) {
                double[] pixels=TrainDisplayClassifier.Thumbnail(frame.Image);SceneLocation fit=locator.Locate(pixels,.78,true);
                if(!TrainDisplayClassifier.ValidFit(fit)){reading.TrainViewDecision="current-view background unconfirmed";return;}
                TrainDisplayClassifier.Features body=TrainDisplayClassifier.Foreground(pixels,empty,fit);
                if(body.Pixels<300||body.SpanX<50||body.SpanY<12){reading.TrainViewDecision="insufficient current-view car extent";return;}
                double m1=TrainDisplayClassifier.Similarity(body,examples[0].Body),m2=TrainDisplayClassifier.Similarity(body,examples[1].Body);
                reading.TrainViewM1Score=m1;reading.TrainViewM2Score=m2;
                double best=Math.Max(m1,m2),margin=Math.Abs(m1-m2),other=m1>m2?reading.TrainM2Score:reading.TrainM1Score;
                if(best<.90||margin<.16||best-other<.18){reading.TrainViewDecision="current-view train pattern not confirmed";return;}
                Example winner=examples[m1>m2?0:1];
                reading.TrainViewDecision="confirmed current-view train example";
                reading.TrainDisplayLabel=winner.Label;reading.TrainExample=winner.Name;
                reading.TrainDecision="current_view_car_pattern";reading.TrainAcceptanceMode="current_view_example";
                reading.TrainMatchSceneScore=fit.Score;reading.TrainMatchBodyPixels=body.Pixels;
            }
        }
    }
}
