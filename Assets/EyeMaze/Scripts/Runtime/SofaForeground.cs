using UnityEngine;

namespace EyeMaze
{
    /// <summary>손님이 앉아 있을 때 소파 앞부분을 인물 앞에 표시한다.</summary>
    public class SofaForeground : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private SpriteRenderer _foreground;
        [SerializeField] private FieldNPC[] _occupants;

        void LateUpdate()
        {
            bool visible = false;
            foreach (FieldNPC occupant in _occupants)
            {
                if (occupant.Present && occupant.IsSeated)
                {
                    visible = true;
                    break;
                }
            }
            _foreground.enabled = visible;
        }
    }
}
