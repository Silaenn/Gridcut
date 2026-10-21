using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GridCut.Game
{
    /// <summary>
    /// Scene Boot (spesifikasi "Loading"): memuat save + tema, data level, dan
    /// menyiapkan layanan portal, lalu membuka Main. Visual: latar warna tema 1,
    /// nama game, bar progres tipis — tanpa animasi berat. Main tetap bisa
    /// dijalankan sendiri saat dev karena AppRoot melakukan inisialisasi serupa.
    /// </summary>
    public sealed class BootController : MonoBehaviour
    {
        public RectTransform BarFill;   // anchorMin (0,0); lebar = anchorMax.x * track

        const string NextScene = "Main";

        IEnumerator Start()
        {
            float[] third = { 1f / 3f, 2f / 3f, 1f };
            for (int i = 0; i < 3; i++)
            {
                switch (i)
                {
                    case 0: SaveStore.Load(); break;        // save + tema tersimpan
                    case 1: LevelRepository.Load(); break;  // data 150 level
                    case 2: PreparePortal(); break;          // SDK portal (#if)
                }
                if (BarFill != null) BarFill.anchorMax = new Vector2(third[i], 1f);
                yield return null;
            }
            yield return null;   // satu frame agar bar penuh sempat terbaca
            SceneManager.LoadScene(NextScene);
        }

        static void PreparePortal()
        {
#if GRIDCUT_CRAZYGAMES || GRIDCUT_COOLMATH || GRIDCUT_ARMORGAMES
            // bootstrap SDK portal dipasang pada tahap integrasi; tanpa portal = no-op
#endif
        }
    }
}
