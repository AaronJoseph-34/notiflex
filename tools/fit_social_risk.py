"""Fit a bounded monotone social-risk model on participant-separated outcomes.

CSV columns: participant, headAngle, partnerAngle, exposureSeconds, outcome.
Outcome is an independently annotated binary disengagement event. Requires numpy.
Synthetic runs cannot emit a participant-fitted profile. No raw audio is required.
"""
import argparse,csv,json
from pathlib import Path
import numpy as np


def auc(y,p):
    # Mann–Whitney rank form handles ties without an O(n_positive*n_negative)
    # pair matrix, keeping larger study analyses bounded in memory.
    order=np.argsort(p,kind='stable');rank_sum=0.;i=0
    while i<len(order):
        j=i+1
        while j<len(order) and p[order[j]]==p[order[i]]:j+=1
        rank_sum+=float(y[order[i:j]].sum())*(i+1+j)/2;i=j
    positive=int(y.sum());negative=len(y)-positive
    return float((rank_sum-positive*(positive+1)/2)/(positive*negative))


def fit(rows,heldout,origin):
    ids=np.array([r['participant'] for r in rows]);y=np.array([float(r['outcome']) for r in rows])
    raw=np.array([[float(r[k]) for k in ('headAngle','partnerAngle','exposureSeconds')] for r in rows])
    if not np.isfinite(raw).all() or not np.isfinite(y).all() or not np.isin(y,[0,1]).all():raise ValueError('Finite measurements and binary outcomes required')
    if (raw<0).any() or (raw[:,:2]>180).any() or (raw[:,2]>300).any():raise ValueError('Measurements outside supported units/ranges')
    if any(not s.strip() for s in ids):raise ValueError('Participant IDs required')
    test=np.isin(ids,list(heldout));train=~test
    ntrain=len(set(ids[train]));ntest=len(set(ids[test]))
    if ntrain<2 or ntest<1 or test.sum()<10:raise ValueError('Need at least two training participants, one held-out participant, ten held-out examples')
    if len(set(y[train]))!=2 or len(set(y[test]))!=2:raise ValueError('Both outcomes required in training and held-out data')
    h,p,d=np.clip(raw[:,0]/90,0,1),np.clip(raw[:,1]/90,0,1),np.clip(raw[:,2]/8,0,1)
    x=np.column_stack([np.ones(len(rows)),h,p,d,p*d]);beta=np.array([-2.,1,1,1,1])
    weights=np.array([1/(ids[train]==s).sum() for s in ids[train]]);weights/=weights.sum()
    for i in range(12000):
        predicted=1/(1+np.exp(-np.clip(x[train]@beta,-20,20)))
        gradient=x[train].T@((predicted-y[train])*weights)+.002*np.r_[0,beta[1:]]
        beta-=.3*gradient;beta[0]=np.clip(beta[0],-20,20);beta[1:]=np.clip(beta[1:],0,20)
    predicted=1/(1+np.exp(-np.clip(x[test]@beta,-20,20)));brier=float(np.mean((predicted-y[test])**2));area=auc(y[test],predicted)
    baseline=float(np.mean((y[train].mean()-y[test])**2))
    accepted=origin=='participant_study' and brier<baseline and area>=.5
    profile=dict(schema=1,id='participant_logistic' if accepted else 'fit_not_accepted',dataOrigin=origin,fitted=accepted,
        wordsPerMinute=180,startReadingSeconds=.8,cueSeconds=.65,chimeSeconds=.25,
        intercept=float(beta[0]),headCoefficient=float(beta[1]),partnerCoefficient=float(beta[2]),durationCoefficient=float(beta[3]),interactionCoefficient=float(beta[4]),
        trainingParticipants=ntrain,testParticipants=ntest,testExamples=int(test.sum()),testBrierScore=brier,testAuc=area)
    return profile,dict(accepted=accepted,heldOutBrier=brier,heldOutAuc=area,trainingPrevalenceBaselineBrier=baseline,
        split='participant-separated',limitations='Conditional disengagement fit, not external validation; reading speed and channel durations remain hypotheses unless independently measured.')


def main():
    p=argparse.ArgumentParser();p.add_argument('csv',type=Path);p.add_argument('--test-participants',required=True);p.add_argument('--data-origin',choices=['participant_study','synthetic'],required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    with a.csv.open(newline='') as f:rows=list(csv.DictReader(f))
    profile,metrics=fit(rows,set(a.test_participants.split(',')),a.data_origin)
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(profile,indent=2)+'\n');a.output.with_suffix('.metrics.json').write_text(json.dumps(metrics,indent=2)+'\n');print(json.dumps(metrics))

if __name__=='__main__':main()
