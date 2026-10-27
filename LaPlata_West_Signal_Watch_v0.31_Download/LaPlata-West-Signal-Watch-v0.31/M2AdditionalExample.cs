using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace LapWest {
    // An optional fallback for the explicitly labeled M2-only frame. This
    // owns its own background registration and never changes baseline fits,
    // templates or accepted headings. No signal pixels or yellow ROI gate it.
    sealed class M2AdditionalExample {
        sealed class Reference {
            public SceneLocator Locator=new SceneLocator(true);
            public double[] Empty;
            public TrainDisplayClassifier.Features Example;
        }
        readonly List<Reference> references=new List<Reference>();
        public string Status="not loaded";
        const string Name="train-m2-mixed.png";
        const double MinimumSimilarity=.92,MinimumM1Separation=.18;
        public M2AdditionalExample(string root) {
            string path=Path.Combine(root,"samples/"+Name);
            try {
                if(!File.Exists(path)){Status="optional reference missing";return;}
                using(Bitmap image=new Bitmap(path)) {
                    double[] pixels=TrainDisplayClassifier.Thumbnail(image);
                    foreach(string empty in new string[]{"off.png","curve-view.jpg"}) {
                        Reference r=new Reference();
                        using(Bitmap background=new Bitmap(Path.Combine(root,"samples/"+empty))) {
                            r.Empty=TrainDisplayClassifier.Thumbnail(background);
                            r.Locator.SetReference(background,new RectangleF(.585f,.19f,.18f,.10f));
                        }
                        SceneLocation fit=r.Locator.Locate(pixels,.78);
                        if(!TrainDisplayClassifier.ValidFit(fit))continue;
                        r.Example=TrainDisplayClassifier.Foreground(pixels,r.Empty,fit);
                        if(r.Example.Pixels<300||r.Example.SpanX<50||r.Example.SpanY<12)continue;
                        r.Locator.Reset();references.Add(r);
                    }
                }
                Status=references.Count>0?"ready":"optional example could not be aligned";
            }catch(Exception ex){references.Clear();Status="optional reference unavailable: "+ex.Message;}
        }
        public void Apply(Bitmap image,Reading reading) {
            reading.TrainAdditionalScore=0;
            if(references.Count==0){reading.TrainAdditionalDecision=Status;return;}
            reading.TrainAdditionalDecision="no qualifying additional match";
            double[] pixels=TrainDisplayClassifier.Thumbnail(image);
            Reference winner=null;SceneLocation winningFit=null;
            TrainDisplayClassifier.Features winningBody=null;
            foreach(Reference r in references) {
                SceneLocation fit=r.Locator.Locate(pixels,.78);
                if(!TrainDisplayClassifier.ValidFit(fit))continue;
                TrainDisplayClassifier.Features body=TrainDisplayClassifier.Foreground(pixels,r.Empty,fit);
                if(body.Pixels<300||body.SpanX<50||body.SpanY<12)continue;
                double score=TrainDisplayClassifier.Similarity(body,r.Example);
                if(score<=reading.TrainAdditionalScore)continue;
                reading.TrainAdditionalScore=score;winner=r;winningFit=fit;winningBody=body;
            }
            if(winner==null||reading.TrainAdditionalScore<MinimumSimilarity)return;
            if(reading.TrainAdditionalScore-reading.TrainM1Score<MinimumM1Separation) {
                reading.TrainAdditionalDecision="additional match too close to M1";return;
            }
            reading.TrainAdditionalDecision="guarded M2-only reference match";
            reading.TrainDisplayLabel="M2 TRAIN";
            reading.TrainDecision="additional_m2_only_example";
            reading.TrainExample=Name;
            reading.TrainAcceptanceMode="guarded_additional_example";
            reading.TrainMatchSceneScore=winningFit.Score;
            reading.TrainMatchBodyPixels=winningBody.Pixels;
        }
    }
}
