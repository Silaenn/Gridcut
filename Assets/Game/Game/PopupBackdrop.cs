using UnityEngine;
using UnityEngine.EventSystems;

namespace GridCut.Game
{
    /// <summary>Klik backdrop popup: Tema/Pengaturan menutup, Konfirmasi tidak.</summary>
    public class PopupBackdrop : MonoBehaviour, IPointerClickHandler
    {
        public PopupView Owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Owner != null) Owner.OnBackdropClick();
        }
    }
}