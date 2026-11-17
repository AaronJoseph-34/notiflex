using System.IO;
using UnityEditor;
using Unity.InferenceEngine;
using UnityEngine;

namespace Notiflex.VR.Editor
{
    public static class SpeechModelBuilder
    {
        public static void Convert()
        {
            foreach(string name in new[]{"VoiceActivity","SpeakerEmbedding"})
            {
                string source="Assets/Notiflex/Editor/SpeechModelImports/"+name+".onnx";
                var asset=AssetDatabase.LoadAssetAtPath<ModelAsset>(source);
                if(asset==null)throw new FileNotFoundException("Speech source model not imported",source);
                var model=ModelLoader.Load(asset);
                ModelWriter.Save("Assets/Resources/Notiflex/"+name+".sentis",model);
                Debug.Log("NOTIFLEX_SPEECH_MODEL "+name+" inputs="+model.inputs.Count+" outputs="+model.outputs.Count);
            }
            AssetDatabase.Refresh();
        }
    }
}
