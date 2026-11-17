# Social-risk calibration protocol

No participant data have been collected for 0.6.0. The shipped model is an unfitted heuristic. The fitting tool and synthetic checks are engineering infrastructure, not a human study or evidence of novelty.

## Outcome and design

Use an approved, consented study in which participants perform face-to-face conversational tasks. Randomize placement, urgency, message length and presentation channel within a balanced design. Include fixed-position/fixed-modality baselines and adaptive Notiflex. Predefine a primary outcome such as independently coded disengagement within a fixed post-notification interval, along with noticeability, response latency, task interruption and conversation measures. Do not use the model's own scores or inferred context as ground-truth outcomes.

Annotators should be blind to condition where feasible. Specify gaze/head-turn criteria, speaking-turn disruption, inter-rater agreement, missing-data rules and exclusions before analysis. Head orientation remains a proxy; do not describe it as eye gaze. Separate the wearer's and partner's reports. A visible person plus microphone evidence is not conversational ground truth: code the actual interaction independently.

Estimate reading speed and cue/chime exposure in separate measurements. The defaults (180 words/min, 0.8 s startup, 0.65 s cue, 0.25 s chime) remain hypotheses until measured. Calibration of the exposure-risk relationship cannot validate the context classifier by itself.

## Input contract

CSV columns are `participant,headAngle,partnerAngle,exposureSeconds,outcome`. Angles are degrees in [0,180], exposure seconds in [0,300], and outcome is an independent binary disengagement event. Participant IDs are pseudonymous grouping keys; never substitute notification IDs or frame IDs. Rows represent independently defined trials, not every correlated video frame.

Run in a local Python environment with NumPy:

```sh
python tools/fit_social_risk.py study.csv \
  --test-participants P09,P10 --data-origin participant_study \
  --output analysis/SocialRiskProfile.json
```

Reserve held-out participants before looking at outcomes. The script requires ≥2 training participants, ≥1 held-out participant, ≥10 held-out trials and both outcomes in both partitions. Those are minimum software guards, not a sample-size/power justification. Choose sample size and participant splits using a study analysis plan. Reusing the held-out set to tune the model invalidates the intended evaluation; reserve a final independent set for external validation.

Features are `H=clamp(headAngle/90)`, `P=clamp(partnerAngle/90)`, `D=clamp(exposureSeconds/8)` and `P×D`. The model is logistic `sigmoid(b0+bH H+bP P+bD D+bPD P D)`. Nonnegative feature coefficients preserve monotonicity; the intercept is bounded to [−20,20]. Projected gradient descent uses L2 coefficient regularization and equal total training weight per participant. Inspect convergence and sensitivity before scientific use; the tool is a reproducible prototype, not an automated statistical review.

Held-out outputs include Brier score, AUC and a training-prevalence baseline Brier score. A profile is accepted for runtime use only for declared participant data, with lower held-out Brier than that baseline and AUC ≥0.5. Otherwise `fitted=false`; do not relabel failed/synthetic runs as validated. Report confidence intervals, participant variation, ablations, calibration curves and external test results in the actual study; these are not supplied by the current small fitting tool.

The runtime's interaction-support multiplier expresses uncertainty that the conversational context applies; the logistic model itself is conditional on conversational trials. Multiplication is a heuristic mixture, not a separately calibrated joint probability. Channel-specific exposure assumptions and generalization beyond the sampled task/population need independent validation.

## Loading and provenance

After reviewing a real accepted profile, place it at `UnityProject/Assets/Resources/Notiflex/SocialRiskProfile.json` and rebuild. Schema, finite ranges and participant/test counts are checked by the runtime. Invalid files retain the default; a valid unfitted file still uses the heuristic cost formula. Profile identity and fit status appear in decision logs. Bundle the anonymized analysis report, model version and held-out results with the research artifact; do not commit participant recordings or identifiable outcomes.

For synthetic verification use `--data-origin synthetic`. Synthetic runs always remain unfitted, regardless of score. The repository's self-test output is explicitly synthetic. Neither software tests nor a fitted profile prove clinical/social benefit or establish a novel method.
