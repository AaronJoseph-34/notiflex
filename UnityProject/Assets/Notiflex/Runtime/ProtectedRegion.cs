using UnityEngine;
namespace Notiflex.VR
{
    public sealed class ProtectedRegion : MonoBehaviour
    {
        public RegionKind kind;
        public Vector2 size=new Vector2(.4f,.4f);
        public bool blocksPresentation=true;
        public Vector3[] Corners()
        {
            Vector3 x=transform.right*size.x/2,y=transform.up*size.y/2,p=transform.position;
            return new[]{p-x-y,p-x+y,p+x-y,p+x+y};
        }
    }

}
