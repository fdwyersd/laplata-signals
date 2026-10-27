LA PLATA WEST CURVE SIGNAL WATCH v0.31


M1 / M2 ALIGNMENT REPAIR IN v0.31
The color reader now keeps landmark-validated camera positions throughout
its wider search. Previously, higher-scoring WRONG positions could crowd
out valid ones before their landmark checks were run. A wider search could
also discard an already validated local position. Both problems are fixed.
Local and wider candidates now share the existing ambiguity check.

This is a tracking change, beyond v0.30's box-visibility change. It does not
move boxes toward whichever colored light happens to be brightest. M2 stays
left of M1 and the boxes remain tied to the calibrated scene or saved manual
positions. The score minimum is still 0.74, with two matching landmarks.
Existing obstruction, mixed-color and visibility checks remain active.

When alignment fails, the picture now shows the COLOR fit score and matched
landmark count. The main View score describes the separate ON/OFF detector.
Hover over M1 or M2 for color fit, patch score and sample details. Export log
now includes color fit method, scale, translation, landmarks and ambiguity.
Dashed boxes are still unconfirmed estimates and never provide color samples.

UPGRADING FROM v0.30
Close the older dashboard. Extract the WHOLE ZIP into a new folder and run
Start-WestSignalWatch.cmd. Keep the DLL, launcher, samples and alert.wav
in that folder. To retain saved preferences or manually positioned boxes,
copy the old folder's data directory into the new folder before first launch.
If you use the per-user fallback settings location, those settings are reused.

VALIDATION OF v0.31
- All 21 supplied examples retain their v0.30 main signal states and train
  headings. The ON/OFF and train callers retain their established matcher.
- All five previously recognized clear color examples retain their colors,
  including M2 GREEN / M1 RED, M2 YELLOW / M1 RED, and RED / RED.
- The reported camera image, reconstructed from the supplied screenshot,
  changes from failed color alignment in v0.30 to a confirmed fit of 0.8966
  with two landmarks and RED / RED in v0.31. Exact overlay colors and the
  dashboard labels were removed from that test reconstruction. Pixels hidden
  beneath those labels cannot be recovered. No raw camera JPEG was supplied.
- All 18 simulated pan/zoom cases from three clear reference views retain
  the expected colors and pass the unchanged alignment requirements.
- Sequential view changes, rejection of blank/out-of-range frames, return
  from a rejected view, manual-box/sample synchronization, independent M2
  movement and saved-settings XML round trip were checked.
- Six Windows Forms integration checks passed: actual RED/RED readout,
  independent color-fit tooltip, diagnostic fields, Move boxes availability
  and synchronized hide/show preference. The actual picture overlay was
  rendered and visually inspected.
- Compiled against Microsoft's .NET 3.5 reference assemblies with C# 3
  language checks. Assembly version 0.31.0.0; CLR image v2.0.50727.

Tests ran on Mono/Linux, not native Windows 7. The screenshot reconstruction
and calibration examples are not an independent live-camera benchmark.
Unrecognized views, foreground obstruction and ambiguous colors can still
show ?. These remain reflected-color estimates, not verified signal aspects.

M1 / M2 BOX VISIBILITY (INTRODUCED IN v0.30)
Click Show boxes to see the M2 signal box on the left and M1 signal box
on the right. M2's label sits above/left; M1's sits below/right so adjacent
labels do not cover each other. Both sample outlines are two pixels wide.
The same label arrangement applies in Move boxes.

Solid sample boxes mark the exact aligned color-sampling rectangles.
When the color-reference alignment fails, the boxes no longer vanish:
dashed boxes labeled M2 signal ? / M1 signal ? show the reader's candidate
positions, with a visible locating message. Those candidate areas are NOT
sampled or treated as confirmed signals. The color readouts remain ?.
This visibility change does not improve a rejected alignment or assign a
new color. Candidate boxes may be displaced until the view can be aligned.
A confirmed alignment is still required to save movable box positions.

Show boxes / Hide boxes controls both confirmed and candidate overlays.
Stale or otherwise unavailable readings never become confirmed samples
just because their geometry is visible.

MOVABLE DETECTION BOXES
1. Wait for a recognized west-curve image, preferably a clear view with no train.
2. Click Move boxes beside the image timestamps. The current image is held,
   boxes are shown and live acquisition/alerts pause while you adjust them.
3. Drag inside the small M2 or M1 box, or drag a Search/Measure border or label.
   Boxes move independently without resizing. They stay within the image.
4. Release the mouse to save that position and preview its actual detection.
5. Click Done moving to resume the previous monitoring/pause mode.

The small M1/M2 boxes change the actual color samples. Search changes the
actual signal-reflection search area. Measure changes the actual ON/OFF
brightness/color measurement area; moving it pins that rectangle to the scene
instead of automatically centering it around whichever reflection is found.
The large normal overlay now marks the actual Search rectangle.
M1/M2 TRAIN identification continues to use the wider picture, not these boxes.
The WEST CURVE TRAIN bright-light check uses the signal/measurement context.

Positions save automatically when the mouse is released and survive restart.
They are stored relative to the registered color-reference scene and projected
for each camera frame, including supported PTZ shifts/zoom and preview margins.
If a saved search/measurement box cannot be aligned, the app shows uncertainty
instead of silently sampling an unrelated location. A recognized background is
required to enter Move boxes. This is manual calibration, not model training.

Escape cancels the current drag; Escape again leaves edit mode. Losing mouse
capture also cancels an unfinished drag. Right-click the camera picture and
choose Reset box positions to restore ALL default/automatic placements. Reset
also works while editing. Hiding boxes leaves edit mode. Positions remain saved.
Changing a placement does not generate sound/toast or advance fresh-image
confirmation. Done moving establishes a silent baseline; a held image does not
count as another new source image. Editing mode itself is not saved on restart.

SHOW BOXES / HIDE BOXES
The button beside Export log shows/hides all debugging outlines and M1/M2
labels. It remembers the choice. Hidden boxes do not pause normal recognition
or the status bar. The synchronized right-click Show boxes menu remains.

ONE-MINUTE SOURCE AND POLLING
The source laplatasignal.jpg is expected to update approximately every
60 seconds. The app polls every 60 seconds, including imported old settings.
The footer shows Source: ~60s and Poll: 60s. Image age uses HTTP Last-Modified
and unchanged source bytes; it does not read the printed camera timestamp.
Re-downloading identical bytes does not advance fresh-image confirmation.
The existing 240-second stale grace remains to allow capture/network timing.

TRAIN HEADINGS TAKE PRIORITY
The first supplied 23:43:47 CDT tank-car image is user-confirmed M1 ONLY.
The later 23:46:46 CDT reflection image is user-confirmed M2.
Those images now produce M1 TRAIN and M2 TRAIN as the main dashboard heading.
A confirmed train heading takes priority over SIGNALS ON, SIGNALS OFF, and
pending/confirmed CHECK IMAGE. Stale-image and connection-error states retain
priority over old train readings. Travel direction is not inferred.

The new train examples use the wider picture and a separate, normalized
empty-view reference. They do not use the yellow signal box or red-reflection
color as a train label. Their optional matcher runs only after the original
recognizer and earlier added M2 matcher have assigned no heading. Existing
accepted headings are preserved. It requires two matching scene landmarks,
background score >=0.80, patch minimum >=0.65, >=300 foreground pixels,
minimum foreground extent, similarity >=0.90, class separation >=0.16, and
separation from the baseline opposite-track score >=0.18. These are similarity
thresholds, not probabilities. Missing/unusable current-view references disable
this optional matcher while retaining prior recognition.

Signal ON/OFF/CHECK IMAGE confirmation and alert logic remain separate from
the train display heading. A train heading itself does not add sound or toast.
Obscured signal colors remain ?. Color changes remain silent.

COLOR TRACKING AND IMAGE GEOMETRY
Some supplied reference screenshots include black side margins from the image
preview. Live camera images use camera-content coordinates. The color reader
now normalizes detected preview margins in references and inputs, then maps
the exact sample rectangles back to the input image for the overlay. Recognized
preview inputs also exclude the exact dashboard annotation colors, including
the observed Windows screenshot color variants. Normal camera frames without
preview margins retain their pixels.

The expanded alignment search now validates landmark candidates before
retaining its best fits. This prevents stronger invalid fits from discarding
a slightly lower-scoring valid alignment. The existing confidence/ambiguity,
visibility, foreground obstruction and mixed-color checks remain in force.
The reported clear 23:47:47 CDT frame now reads M2: RED, M1: RED, including a
camera-content variant without the preview margins. Earlier green/yellow
examples still pass. Large/unrecognized camera views may still show ?.
These remain reflected-color estimates, not certified physical signal aspects.
Upper/lower signal-head order is not inferred.

COLOR SAMPLE BOXES
M2 is the left box; M1 is the adjacent box on the right. Both markers use
exactly the image coordinates sampled by the color reader and move together
with scene tracking. Unconfirmed or obstructed colors still show ?.
All sample/search/measurement boxes can be moved through Move boxes, and
are toggled together by Show boxes /
Hide boxes, or the synchronized right-click menu. Train identification uses
the wider picture and retains priority over signal-state display headings.

START / UPGRADE
1. Close older dashboards.
2. Extract the ENTIRE v0.31 ZIP into a NEW normal user folder.
3. Run Start-WestSignalWatch.cmd in that folder.
Keep its DLL, launcher, samples and alert.wav together. The launcher checks
assembly version 0.31.0.0 and writes startup-error.txt if startup fails.
The app targets classic C# / .NET 3.5 / CLR 2 for Windows 7 and PowerShell 2.
No Python, OpenCV or neural-network runtime is required on the Windows PC.

The camera URL remains http://66.179.190.208/laplatasignal.jpg. The v0.20 HTTP
download code, request headers and timeouts are retained. Two accidentally
shipped .218/.238 defaults are repaired when loading saved settings. Custom
URLs remain supported. Settings and logs normally live under data/ beside the
app; a per-user fallback is used if that location is not writable.

CONTROLS AND DIAGNOSTICS
Refresh requests another live image. Pause/Resume controls live polling.
Test OFF/Test ON load supplied samples. Sound/Toast control existing alerts.
Set OFF view is for a confirmed current nighttime OFF scene. Export log saves
decisions, recent source frames and references to a ZIP for troubleshooting.
Train matcher decisions and separate M1/M2 example scores are included.

See the v0.31 validation notes at the top of this file.
