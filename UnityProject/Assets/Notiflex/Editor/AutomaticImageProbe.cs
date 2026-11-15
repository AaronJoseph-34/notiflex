using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;

namespace Notiflex.VR.Editor
{
    // Optional offline model probe. Input files are RGB NCHW float32 [0,1],
    // resized to model dimensions, with a top-left image origin. No photos are
    // copied into the repository or APK. This does not test live-camera alignment.
    public static class AutomaticImageProbe
    {
        [Serializable] public class Result {public string image;public int[] classes;public float[] scores;public float[] bestScoreByClass;}
        [Serializable] public class Report {public Result[] results;}
        [Serializable] public class Mapping {public float x,y,width,height;}
        public static void Run(string tensorDirectory)
        {
            var model=ModelLoader.Load(Resources.Load<ModelAsset>("Notiflex/ContextDetector"));
            int h=model.inputs[0].shape.Get(2),w=model.inputs[0].shape.Get(3);
            using var worker=new Worker(model,BackendType.CPU);
            var results=new List<Result>();
            foreach(var path in Directory.GetFiles(tensorDirectory,"*.f32"))
            {
                byte[] bytes=File.ReadAllBytes(path);
                if(bytes.Length!=3*h*w*sizeof(float))throw new ArgumentException("Image tensor has wrong dimensions");
                var data=new float[3*h*w];Buffer.BlockCopy(bytes,0,data,0,bytes.Length);
                using var input=new Tensor<float>(new TensorShape(1,3,h,w),data);worker.Schedule(input);
                var boxes=worker.PeekOutput(0) as Tensor<float>;var ids=worker.PeekOutput(1) as Tensor<int>;var scores=worker.PeekOutput(2) as Tensor<float>;
                boxes.CompleteAllPendingOperations();ids.CompleteAllPendingOperations();scores.CompleteAllPendingOperations();
                var detections=new List<CameraDetection>();
                Rect content=new Rect(0,0,1,1);
                string mappingPath=Path.ChangeExtension(path,"json");
                if(File.Exists(mappingPath))
                {var m=JsonUtility.FromJson<Mapping>(File.ReadAllText(mappingPath));content=new Rect(m.x,m.y,m.width,m.height);}
                var classIds=ids.DownloadToArray();var objectScores=scores.DownloadToArray();var best=new float[80];
                for(int i=0;i<classIds.Length;i++)if(classIds[i]>=0&&classIds[i]<80)best[classIds[i]]=Mathf.Max(best[classIds[i]],objectScores[i]);
                AutomaticContext.Decode(boxes.DownloadToArray(),classIds,objectScores,w,h,
                    new Vector3(-1,-1,1),Vector3.right*2,Vector3.up*2,detections,content);
                results.Add(new Result {image=Path.GetFileNameWithoutExtension(path),classes=detections.ConvertAll(d=>d.classId).ToArray(),scores=detections.ConvertAll(d=>d.score).ToArray(),bestScoreByClass=best});
            }
            string dir=Path.GetFullPath(Application.dataPath+"/../../evidence/automatic-mode");Directory.CreateDirectory(dir);
            File.WriteAllText(dir+"/reference-image-probe.json",JsonUtility.ToJson(new Report{results=results.ToArray()},true));
            Debug.Log("NOTIFLEX_REFERENCE_IMAGES "+results.Count);
        }
    }
}
