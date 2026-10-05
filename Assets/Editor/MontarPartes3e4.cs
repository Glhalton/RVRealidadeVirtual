#if UNITY_EDITOR
using System.Linq;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Menu RV > Montar Partes 3 e 4: ajusta o cenário (bancada 2x1x1 e cubo 0,2 do enunciado, com texturas),
/// monta o PNJ (NavMesh) e a arquitetura de rede (NGO). É idempotente: pode ser executado várias vezes.
/// </summary>
public static class MontarPartes3e4
{
    const string CenaPath = "Assets/Scenes/VRScene.unity";
    const string PastaPrefabs = "Assets/Prefabs";
    const string PrefabPath = "Assets/Prefabs/CuboInterativo.prefab";
    const string NavMeshPath = "Assets/Scenes/NavMesh-VRScene.asset";

    const float AlturaPNJ = 1.7f;   // metros

    [MenuItem("RV/Montar Partes 3 e 4")]
    public static void Montar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(CenaPath, OpenSceneMode.Single);

        var chao = Achar("Chão", "Chao");
        var cubo = Achar("Manipulável", "Manipulavel");
        if (chao == null || cubo == null)
        {
            EditorUtility.DisplayDialog("RV", "Não encontrei 'Chão' e/ou 'Manipulável' na VRScene.", "OK");
            return;
        }

        AjustarCenario(chao, Achar("Mesa"), cubo);   // antes do bake e do prefab
        ParteTres(chao);
        ParteQuatro(cubo);
        AjustarAlturaDoUsuario();
        AdicionarCenaAoBuild();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("RV", "Partes 3 e 4 montadas e cena salva.\nAperte Play para testar.", "OK");
    }

    // ---------------- Cenário (Parte 1, medidas do enunciado) ----------------
    static void AjustarCenario(GameObject chao, GameObject mesa, GameObject cubo)
    {
        // Chão com piso de placas (plano 10x10 m, uma repetição por metro)
        Pintar(chao, Mat("Mat_Chao", Color.white, 0.15f, Tex("Piso"), new Vector2(10f, 10f)));

        // Bancada: cubo escalado (2,1,1), apoiada no chão (y = 0,5), em frente ao usuário
        var madeira = Mat("Mat_Mesa", Color.white, 0.30f, Tex("Madeira"), new Vector2(2f, 1f));
        if (mesa != null)
        {
            mesa.transform.rotation = Quaternion.identity;
            mesa.transform.localScale = new Vector3(2f, 1f, 1f);
            mesa.transform.position = new Vector3(2.11f, 0.5f, 2.1f);
            Pintar(mesa, madeira);
            EditorUtility.SetDirty(mesa);
        }

        // Cubo interativo 0,2 m sobre a bancada (topo da bancada em y = 1)
        cubo.transform.rotation = Quaternion.identity;
        cubo.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        cubo.transform.position = new Vector3(2.11f, 1.11f, 1.8f);
        Pintar(cubo, Mat("Mat_Cubo", new Color(0.96f, 0.55f, 0.12f), 0.50f));
        EditorUtility.SetDirty(cubo);
    }

    static Texture2D Tex(string nome)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{nome}.png");
        if (t == null) Debug.LogWarning($"[RV] Textura Assets/Textures/{nome}.png não encontrada; usando cor lisa.");
        return t;
    }

    static Material Mat(string nome, Color cor, float suavidade, Texture2D textura = null, Vector2? tiling = null)
    {
        string path = $"Assets/Materials/{nome}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = cor;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", suavidade);
        if (textura != null)
        {
            m.mainTexture = textura;
            m.mainTextureScale = tiling ?? Vector2.one;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Pintar(GameObject go, Material m)
    {
        var r = go.GetComponent<MeshRenderer>();
        if (r == null) return;
        r.sharedMaterial = m;
        EditorUtility.SetDirty(r);
    }

    // ---------------- Parte 3 ----------------
    static void ParteTres(GameObject chao)
    {
        // NavMeshSurface (substitui o antigo Window > AI > Navigation do enunciado)
        var ambiente = chao.transform.parent != null ? chao.transform.parent.gameObject : chao;
        var surface = ambiente.GetComponent<NavMeshSurface>();
        if (surface == null) surface = ambiente.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.BuildNavMesh();

        if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);   // persiste o bake na cena
        EditorUtility.SetDirty(surface);

        // PNJ_Assistente: a cápsula do enunciado fica como corpo lógico (colisor + agente);
        // o visual de pessoa é um modelo filho.
        float meia = AlturaPNJ / 2f;
        var pnj = GameObject.Find("PNJ_Assistente");
        if (pnj == null)
        {
            pnj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pnj.name = "PNJ_Assistente";
            Undo.RegisterCreatedObjectUndo(pnj, "Criar PNJ");

            // ~5 m do usuário, para o comportamento aparecer assim que o Play começar
            var pos = chao.transform.position + new Vector3(-2f, meia, -2f);
            if (NavMesh.SamplePosition(pos, out var hit, 5f, NavMesh.AllAreas)) pos = hit.position + Vector3.up * meia;
            pnj.transform.position = pos;
        }

        pnj.transform.localScale = Vector3.one;
        var cc = pnj.GetComponent<CapsuleCollider>();
        if (cc != null) { cc.radius = 0.25f; cc.height = AlturaPNJ; cc.center = Vector3.zero; }
        var rend = pnj.GetComponent<MeshRenderer>();
        if (rend != null) rend.enabled = false;          // o que aparece é o modelo filho

        ConstruirModelo(pnj);

        var agent = pnj.GetComponent<NavMeshAgent>();
        if (agent == null) agent = pnj.AddComponent<NavMeshAgent>();
        agent.radius = 0.25f;
        agent.height = AlturaPNJ;
        agent.baseOffset = meia;      // pivô da cápsula está no centro
        agent.stoppingDistance = 0.5f;

        var comp = pnj.GetComponent<ComportamentoPNJ>();
        if (comp == null) comp = pnj.AddComponent<ComportamentoPNJ>();
        if (Camera.main != null) comp.alvoUsuario = Camera.main.transform;
        EditorUtility.SetDirty(pnj);
    }

    // Pessoa estilizada montada com formas simples (sem colisores).
    static void ConstruirModelo(GameObject pnj)
    {
        for (int i = pnj.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(pnj.transform.GetChild(i).gameObject);

        var modelo = new GameObject("Modelo");
        modelo.transform.SetParent(pnj.transform, false);

        var camisa = Mat("Mat_PNJ_Camisa", new Color(0.15f, 0.55f, 0.75f), 0.30f);
        var calca = Mat("Mat_PNJ_Calca", new Color(0.16f, 0.18f, 0.30f), 0.25f);
        var sapato = Mat("Mat_PNJ_Sapato", new Color(0.08f, 0.08f, 0.10f), 0.40f);
        var pele = Mat("Mat_PNJ_Pele", new Color(0.93f, 0.74f, 0.60f), 0.20f);
        var cabelo = Mat("Mat_PNJ_Cabelo", new Color(0.20f, 0.13f, 0.08f), 0.20f);
        var olho = Mat("Mat_PNJ_Olho", new Color(0.05f, 0.05f, 0.07f), 0.60f);

        // y é medido a partir dos pés; (x, z) em relação ao centro. Tamanhos são a caixa envolvente em metros.
        Parte(modelo, "PernaEsq", PrimitiveType.Capsule, -0.09f, 0.41f, 0f, 0.14f, 0.82f, 0.14f, calca);
        Parte(modelo, "PernaDir", PrimitiveType.Capsule, 0.09f, 0.41f, 0f, 0.14f, 0.82f, 0.14f, calca);
        Parte(modelo, "SapatoEsq", PrimitiveType.Cube, -0.09f, 0.035f, 0.05f, 0.12f, 0.07f, 0.26f, sapato);
        Parte(modelo, "SapatoDir", PrimitiveType.Cube, 0.09f, 0.035f, 0.05f, 0.12f, 0.07f, 0.26f, sapato);
        Parte(modelo, "Quadril", PrimitiveType.Capsule, 0f, 0.86f, 0f, 0.36f, 0.26f, 0.21f, calca);
        Parte(modelo, "Tronco", PrimitiveType.Capsule, 0f, 1.12f, 0f, 0.38f, 0.56f, 0.22f, camisa);
        Parte(modelo, "BracoEsq", PrimitiveType.Capsule, -0.245f, 1.09f, 0f, 0.09f, 0.60f, 0.09f, camisa);
        Parte(modelo, "BracoDir", PrimitiveType.Capsule, 0.245f, 1.09f, 0f, 0.09f, 0.60f, 0.09f, camisa);
        Parte(modelo, "MaoEsq", PrimitiveType.Sphere, -0.245f, 0.77f, 0f, 0.09f, 0.09f, 0.09f, pele);
        Parte(modelo, "MaoDir", PrimitiveType.Sphere, 0.245f, 0.77f, 0f, 0.09f, 0.09f, 0.09f, pele);
        Parte(modelo, "Pescoco", PrimitiveType.Cylinder, 0f, 1.44f, 0f, 0.09f, 0.08f, 0.09f, pele);
        Parte(modelo, "Cabeca", PrimitiveType.Sphere, 0f, 1.57f, 0f, 0.20f, 0.24f, 0.21f, pele);
        Parte(modelo, "Cabelo", PrimitiveType.Sphere, 0f, 1.655f, -0.01f, 0.215f, 0.14f, 0.225f, cabelo);
        Parte(modelo, "OlhoEsq", PrimitiveType.Sphere, -0.045f, 1.565f, 0.097f, 0.035f, 0.035f, 0.035f, olho);
        Parte(modelo, "OlhoDir", PrimitiveType.Sphere, 0.045f, 1.565f, 0.097f, 0.035f, 0.035f, 0.035f, olho);
        Parte(modelo, "Nariz", PrimitiveType.Sphere, 0f, 1.535f, 0.105f, 0.03f, 0.04f, 0.035f, pele);
    }

    static void Parte(GameObject pai, string nome, PrimitiveType tipo,
                      float x, float yPes, float z, float tx, float ty, float tz, Material mat)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nome;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);   // só visual: não interfere na física nem no NavMesh

        go.transform.SetParent(pai.transform, false);
        go.transform.localPosition = new Vector3(x, yPes - AlturaPNJ / 2f, z);

        // Capsule e Cylinder do Unity têm altura 2 com escala 1; Sphere e Cube têm 1.
        float escY = (tipo == PrimitiveType.Capsule || tipo == PrimitiveType.Cylinder) ? ty / 2f : ty;
        go.transform.localScale = new Vector3(tx, escY, tz);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ---------------- Parte 4 ----------------
    static void ParteQuatro(GameObject cubo)
    {
        if (!AssetDatabase.IsValidFolder(PastaPrefabs)) AssetDatabase.CreateFolder("Assets", "Prefabs");

        // Cubo interativo: NetworkObject + NetworkTransform (exatamente o que o enunciado pede)
        if (!PrefabUtility.IsPartOfPrefabInstance(cubo))
        {
            if (cubo.GetComponent<NetworkObject>() == null) cubo.AddComponent<NetworkObject>();
            if (cubo.GetComponent<NetworkTransform>() == null) cubo.AddComponent<NetworkTransform>();
            PrefabUtility.SaveAsPrefabAssetAndConnect(cubo, PrefabPath, InteractionMode.AutomatedAction);
        }
        CorrigirPrefabDoCubo();

        // NetworkManager + Unity Transport
        var nmGO = GameObject.Find("NetworkManager");
        if (nmGO == null)
        {
            nmGO = new GameObject("NetworkManager");
            Undo.RegisterCreatedObjectUndo(nmGO, "Criar NetworkManager");
        }

        var nm = nmGO.GetComponent<NetworkManager>();
        if (nm == null) nm = nmGO.AddComponent<NetworkManager>();
        var utp = nmGO.GetComponent<UnityTransport>();
        if (utp == null) utp = nmGO.AddComponent<UnityTransport>();
        if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
        nm.NetworkConfig.NetworkTransport = utp;

        if (nmGO.GetComponent<RedeHUD>() == null) nmGO.AddComponent<RedeHUD>();
        EditorUtility.SetDirty(nmGO);
    }

    // Edita o próprio asset do prefab (a instância na cena herda a mudança).
    static void CorrigirPrefabDoCubo()
    {
        var raiz = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // NetworkRigidbody deixava o Rigidbody kinematic (sem gravidade) fora de uma sessão de rede.
            var nrb = raiz.GetComponent<NetworkRigidbody>();
            if (nrb != null) Object.DestroyImmediate(nrb);

            var no = raiz.GetComponent<NetworkObject>();
            if (no == null) no = raiz.AddComponent<NetworkObject>();
            if (raiz.GetComponent<NetworkTransform>() == null) raiz.AddComponent<NetworkTransform>();

            // Evita o erro "networkManager is not listening ... before re-parenting" quando o XR
            // Grab Interactable mexe no pai do cubo antes de existir uma sessão de rede.
            var so = new SerializedObject(no);
            var p = so.FindProperty("AutoObjectParentSync");
            if (p != null)
            {
                p.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    // ---------------- Usuário ----------------
    static void AjustarAlturaDoUsuario()
    {
        var origem = GameObject.Find("XR Origin");
        if (origem != null && origem.GetComponent<AlturaDoUsuario>() == null)
            origem.AddComponent<AlturaDoUsuario>();
    }

    static void AdicionarCenaAoBuild()
    {
        var cenas = EditorBuildSettings.scenes.ToList();
        if (cenas.Any(s => s.path == CenaPath)) return;
        cenas.Add(new EditorBuildSettingsScene(CenaPath, true));
        EditorBuildSettings.scenes = cenas.ToArray();
    }

    static GameObject Achar(params string[] nomes)
    {
        foreach (var n in nomes)
        {
            var go = GameObject.Find(n);
            if (go != null) return go;
        }
        return null;
    }
}
#endif
