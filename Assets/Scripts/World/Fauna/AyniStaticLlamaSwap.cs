using UnityEngine;

namespace Ayni.World.Fauna
{
    /// <summary>
    /// Las llamas que se colocaron con la malla Llama.obj (objetos "Llama", malla "Llama_Mesh") no tienen esqueleto
    /// y se quedaban quietas como estatuas junto al camino. Al empezar el nivel se ocultan y en su sitio se crea una
    /// llama de la fauna procedural de Jhosue (AyniCamelidModel + AyniGrazingAnimal), que pasta, mira, camina en paso
    /// de andadura y se aparta de Yari. La escena no cambia. SifuCombatHUD lo añade solo.
    /// </summary>
    public class AyniStaticLlamaSwap : MonoBehaviour
    {
        [SerializeField] private float grazeRadius = 7f;
        [SerializeField] private float walkSpeed = 0.8f;
        [SerializeField] private float alertRadius = 5.5f;
        [SerializeField] private float runSpeed = 3f;

        private void Start()
        {
            Transform group = null;
            int index = 0;
            foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.name.Contains("Llama_Mesh")) continue;
                Transform statue = filter.transform;
                Vector3 position = statue.position;
                float yaw = statue.eulerAngles.y;
                statue.gameObject.SetActive(false);

                if (group == null) group = new GameObject("Ayni_LlamasAnimadas").transform;
                if (!AyniGrazingAnimal.IsOpenGround(position, 30f, out float groundY)) groundY = position.y;
                var parts = AyniCamelidModel.Build(group, $"Llama_Animada_{index + 1}", AyniCamelidSpecies.Llama, 9100 + index * 17);
                parts.root.transform.SetPositionAndRotation(new Vector3(position.x, groundY, position.z), Quaternion.Euler(0f, yaw, 0f));
                var grazing = parts.root.AddComponent<AyniGrazingAnimal>();
                grazing.Configure(new Vector3(position.x, 0f, position.z), grazeRadius, walkSpeed, parts);
                grazing.ConfigureAlert(alertRadius, runSpeed);
                index++;
            }
            if (index > 0) Debug.Log($"[Ayni Fauna] {index} llamas de Llama.obj cambiadas por llamas animadas");
        }
    }
}
