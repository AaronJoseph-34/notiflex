"""Independent Kaldi-fbank and ONNX Runtime contract fixtures; synthetic signals only."""
import argparse,json
from pathlib import Path
import numpy as np
import kaldi_native_fbank as knf
import onnxruntime as ort
p=argparse.ArgumentParser();p.add_argument('--cache',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
i=np.arange(32240,dtype=np.float64)
wave=(np.sin(i*.023)*(.3+.2*np.sin(i*.001))+.01*np.cos(i*.37)).astype(np.float32)
opts=knf.FbankOptions();opts.frame_opts.dither=0;opts.frame_opts.window_type='hamming';opts.mel_opts.num_bins=80
fbank=knf.OnlineFbank(opts);fbank.accept_waveform(16000,(wave*32768).tolist());fbank.input_finished()
features=np.stack([fbank.get_frame(i) for i in range(fbank.num_frames_ready)]).astype(np.float32);assert features.shape==(200,80)
features-=features.mean(axis=0,keepdims=True)
x=np.sin(np.arange(16000,dtype=np.float64)*.017).astype(np.float32).reshape(1,200,80)
speaker=ort.InferenceSession(str(a.cache/'speaker.onnx'),providers=['CPUExecutionProvider'])
emb=speaker.run(None,{'feats':x})[0].flatten()
vad=ort.InferenceSession(str(a.cache/'silero.onnx'),providers=['CPUExecutionProvider'])
v=vad.run(None,{'input':np.zeros((1,576),np.float32),'state':np.zeros((2,1,128),np.float32),'sr':np.array(16000,np.int64)})
a.output.parent.mkdir(parents=True,exist_ok=True)
a.output.write_text(json.dumps({'reference':'kaldi-native-fbank 1.23.1 / ONNX Runtime synthetic contract; not accuracy validation','features':features.flatten().tolist(),'speaker':emb.tolist(),'vad':float(v[0].flatten()[0]),'state':v[1].flatten().tolist()},separators=(',',':'))+'\n')
