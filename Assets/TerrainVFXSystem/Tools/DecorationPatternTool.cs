using UnityEditor;
using UnityEngine;
using System;
using System.IO;

public class DecorationPatternTool : EditorWindow
{
    [MenuItem("Window/Environment/DecorationPatternTool", false, 4000)]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(DecorationPatternTool));
    }

    enum PatternType
    {
        // jittered grid based
        Fill,
        Rows,
        Columns,
        Border,
        Alternating,

        // sequence based
        Halton,
        Golden,
        Plastic,
    }

    bool LoadBackground()
    {
        bool changed = false;
        if (m_Placement == null)
        {
            m_Placement = (Texture2D)Resources.Load("Circle");
            changed = true;
        }
        if (m_Background == null)
        {
            m_Background = (Texture2D)Resources.Load("Background");
            changed = true;
        }
        return changed;
    }

    // Some very quickly hacked together visualization and code for building some test textures.
    // A proper tool needs to be able to allow full control over pattern generation including:
    // -Per texel jittering.
    // -Ability to make better tiling patterns.
    // -Save and load textures.
    // -Automatically set correct import options for generated textures.
    // -Dithering controls (setting the threshold value).
    // -Determine the *minimum* distance between 2 texels, so that we can have code that sets tiling size based on prefab bounds (we can automatically set the footpint).
    public void OnGUI()
    {
        bool changed = false;

        EditorGUI.BeginChangeCheck();
        GUILayout.BeginVertical();

        m_PatternWidth = EditorGUILayout.IntField("Pattern Width", m_PatternWidth);
        m_PatternHeight = EditorGUILayout.IntField("Pattern Height", m_PatternHeight);

        m_PatternType = (PatternType)EditorGUILayout.EnumPopup("Pattern Type", m_PatternType);
        if (m_PatternType >= PatternType.Halton)
        {
            m_SampleCount = EditorGUILayout.IntField("Sample Count", m_SampleCount);
        }
        else
        {
            m_JitterX = EditorGUILayout.Slider("Jitter X", m_JitterX, 0.0f, 1.0f);
            m_JitterY = EditorGUILayout.Slider("Jitter Y", m_JitterY, 0.0f, 1.0f);
        }

        changed = GUILayout.Button("Generate");
        if (LoadBackground())
        {
            changed = true;
        }

        GUILayout.EndVertical();
        changed = changed || EditorGUI.EndChangeCheck();
        if (m_Colors == null || (m_Colors.Length != (m_PatternWidth * m_PatternHeight)))
        {
            Array.Resize(ref m_Colors, m_PatternWidth * m_PatternHeight);
            changed = true;
        }

        GUILayout.BeginHorizontal();
        bool optim = GUILayout.Button("Optimize");
        bool optim10 = GUILayout.Button("Optimize 10");
        bool optim100 = GUILayout.Button("Optimize 100");
        GUILayout.EndHorizontal();

        bool exportPattern = GUILayout.Button("Export Pattern");
        bool importPattern = GUILayout.Button("Import Pattern");
        if (exportPattern)
        {
            var path = EditorUtility.SaveFilePanel("Save pattern as png", "", "Pattern.png", "png");
            if (path.Length != 0)
            {
                Texture2D exported = new Texture2D(m_PatternWidth, m_PatternHeight, TextureFormat.RGBA32, false, true);
                exported.SetPixels(m_Colors);
                var exrData = exported.EncodeToPNG();
                if (exrData != null)
                {
                    File.WriteAllBytes(path, exrData);
                }
                UnityEngine.Object.DestroyImmediate(exported);
            }
        }
        if (importPattern)
        {
            var path = EditorUtility.OpenFilePanel("Load pattern png", "", "png");
            if (path.Length != 0)
            {
                Texture2D importedTexture = new Texture2D(1, 1);
                var fileContent = File.ReadAllBytes(path);
                importedTexture.LoadImage(fileContent);
                m_PatternWidth = importedTexture.width;
                m_PatternHeight = importedTexture.height;
                var colors = importedTexture.GetPixels();
                Array.Resize(ref m_Colors, colors.Length);
                Array.Copy(colors, m_Colors, colors.Length);
                UnityEngine.Object.DestroyImmediate(importedTexture);
            }
        }

        if (changed)
        {
            if (m_Placement == null) return;
            if (m_Background == null) return;

            float deltaX = 1.0f / m_PatternWidth;
            float deltaY = 1.0f / m_PatternHeight;

            if (m_PatternType == PatternType.Halton)
            {
                GenerateHaltonPattern(2, 3, m_SampleCount);
            }
            else if (m_PatternType == PatternType.Golden)
            {
                GenerateGoldenRatioPattern(m_SampleCount);
            }
            else if (m_PatternType == PatternType.Plastic)
            {
                GeneratePlasticPattern(m_SampleCount);
            }
            else
            {
                GenerateRandomPattern();
            }
        }

        if (optim)
        {
            Optimize();
        }
        if (optim10)
        {
            for (int i = 0; i < 10; i++)
                Optimize();
        }
        if (optim100)
        {
            for (int i = 0; i < 100; i++)
                Optimize();
        }

        m_PlacementSize = EditorGUILayout.IntField("Placement Size", m_PlacementSize);
        m_PlacementSpacing = EditorGUILayout.IntField("Placement Spacing", m_PlacementSpacing);
        m_Threshold = Mathf.RoundToInt(EditorGUILayout.Slider("Threshold", (float)m_Threshold, 0.0f, 256.0f));

        float yPos = 260.0f;

        GUI.DrawTexture(new Rect(0, yPos, m_PlacementSpacing * m_PatternWidth, m_PlacementSpacing * m_PatternHeight), m_Background);
        float halfPointSize = 0.5f * m_PlacementSize;
        for (int i = 0; i < m_Colors.Length; ++i)
        {
            var color = m_Colors[i];
            if ((color.a > 0) && (color.b < m_Threshold / 256.0f))
            {
                float deltaX = color.r;
                float deltaY = color.g;
                float x = m_PlacementSpacing * (i % m_PatternWidth + deltaX) - halfPointSize;
                float y = yPos + m_PlacementSpacing * (i / m_PatternWidth + deltaY) - halfPointSize;

                GUI.DrawTexture(new Rect(x, y, m_PlacementSize, m_PlacementSize), m_Placement);
            }
        }
    }

    void GenerateGoldenRatioPattern(int numSamples)
    {
        Vector2[] points = new Vector2[numSamples];

        // set the initial first coordinate
        double x = UnityEngine.Random.value;

        // tracking for the minimum
        double min = x;
        uint idx = 0;

        // set the first coordinates
        for (uint i = 0; i < numSamples; i++)
        {
            points[i][1] = (float) x;

            // record the minimum
            if (x < min)
            {
                min = x;
                idx = i;
            }

            // increment the coordinate and wrap
            x += 0.618033988749894;
            if (x >= 1.0)
            {
                x -= 1.0;
            }
        }

        // find the first Fibonacci >= N
        uint f = 1;
        uint fp = 1;
        uint parity = 0;
        while (f + fp < numSamples)
        {
            uint tmp = f;
            f += fp;
            fp = tmp;
            ++parity;
        }

        // set the increment and decrement
        uint inc = fp;
        uint dec = f;
        if ((parity & 1) != 0)
        {
            inc = f;
            dec = fp;
        }

        // permute the first coordinates
        points[0][0] = points[idx][1];
        for (uint i = 1; i < numSamples; ++i)
        {
            if (idx < dec)
            {
                idx += inc;
                if (idx >= numSamples)
                {
                    idx = idx - dec;
                }
            }
            else
            {
                idx = idx - dec;
            }

            points[i][0] = points[idx][1];
        }

        // set the initial second coordinate
        double y = UnityEngine.Random.value;

        // set the second coordinates to pure golden ratio sequence
        for (uint i = 0; i < numSamples; ++i)
        {
            points[i][1] = (float) y;

            // increment the coordinate
            y += 0.618033988749894;

            if (y >= 1.0)
            {
                y -= 1.0;
            }
        }

        // clear to zero
        for (int ay = 0; ay < m_PatternHeight; ++ay)
        {
            for (int ax = 0; ax < m_PatternWidth; ++ax)
            {
                m_Colors[ay * m_PatternWidth + ax] = new Color(0, 0, 0, 0);
            }
        }

        for(int i = 0; i < numSamples; i++)
        {
            Vector2 s = points[i];
            s *= new Vector2(m_PatternWidth, m_PatternHeight);

            int px = Mathf.FloorToInt(s.x);
            int py = Mathf.FloorToInt(s.y);
            s.x -= px;
            s.y -= py;

            byte threshold = (byte)(i * 255.9f / numSamples);
            byte jitterX = (byte)(s.x * 255.0f);
            byte jitterY = (byte)(s.y * 255.0f);

            if (m_Colors[py * m_PatternWidth + px].a <= 0.0f)
                m_Colors[py * m_PatternWidth + px] = new Color32(jitterX, jitterY, threshold, 1);
        }
    }

    interface ISequence2D
    {
        Vector2 Next();
    }

    class HaltonSequence2D : ISequence2D
    {
        int basex;
        int basey;
        int i;
        public HaltonSequence2D(int basex, int basey)
        {
            this.basex = basex;
            this.basey = basey;
            this.i = 0;
        }

        public Vector2 Next()
        {
            i++;

            float x = 0.0f;
            {
                float denX = (float)basex;
                int n = i;
                while (n > 0)
                {
                    int multiplier = n % basex;
                    x += ((float)multiplier) / denX;
                    n = n / basex;
                    denX *= basex;
                }
            }

            float y = 0.0f;
            {
                float denY = (float)basey;
                int n = i;
                while (n > 0)
                {
                    int multiplier = n % basey;
                    y += ((float)multiplier) / denY;
                    n = n / basey;
                    denY *= basey;
                }
            }

            return new Vector2(x, y);
        }
    }

    void GenerateHaltonPattern(int basex, int basey, int numSamples)
    {
        HaltonSequence2D haltonSequence = new HaltonSequence2D(basex, basey);
        GenerateSequencePattern(haltonSequence, numSamples);
    }

    class PlasticSequence2D : ISequence2D
    {
        // plastic constant
        const double g = 1.32471795724474602596090885447809;
        const double a1 = 1.0 / g;
        const double a2 = 1.0 / (g * g);

        double sx = 0.5;
        double sy = 0.5;

        public Vector2 Next()
        {
            sx += a1;
            sy += a2;
            if (sx > 1.0)
                sx -= 1.0;
            if (sy > 1.0)
                sy -= 1.0;
            return new Vector2((float) sx, (float) sy);
        }
    }

    void GeneratePlasticPattern(int numSamples)
    {
        PlasticSequence2D plasticSequence = new PlasticSequence2D();
        GenerateSequencePattern(plasticSequence, numSamples);
    }

    void GenerateSequencePattern(ISequence2D sequence, int numSamples)
    {
        // clear to zero
        for (int y = 0; y < m_PatternHeight; ++y)
        {
            for (int x = 0; x < m_PatternWidth; ++x)
            {
                m_Colors[y * m_PatternWidth + x] = new Color(0, 0, 0, 0);
            }
        }

        int maxPixels = m_PatternHeight * m_PatternWidth;
        if (numSamples > maxPixels)
            numSamples = maxPixels;

        // infinite loop catcher
        // stop after this many total sequence values
        // even if we don't have full sample count yet
        int maxn = maxPixels * 4;
        int n = 0;
        int pointsGenerated = 0;
        while ((pointsGenerated < numSamples) && (n < maxn))
        {
            // build point from sequence value (sx, sy)
            Vector2 s = sequence.Next();
            n++;

            s *= new Vector2(m_PatternWidth, m_PatternHeight);
            int px = Mathf.FloorToInt(s.x);
            int py = Mathf.FloorToInt(s.y);

            if (m_Colors[py * m_PatternWidth + px].a <= 0.0f)
            {
                pointsGenerated++;

                s.x -= px;
                s.y -= py;

                byte threshold = (byte)(pointsGenerated * 255.9f / numSamples);
                byte jitterX = (byte)(s.x * 255.0f);
                byte jitterY = (byte)(s.y * 255.0f);

                m_Colors[py * m_PatternWidth + px] = new Color32(jitterX, jitterY, threshold, 1);
            }
        }
    }

    Vector2 GetPixelPos(int x, int y)
    {
        Color p = m_Colors[y * m_PatternWidth + x];
        return new Vector2(
            x + p.r,        // jitterX
            y + p.g);       // jitterY
    }

    void Optimize()
    {
        // modify each pixel to move away from nearest neighbor
        for (int y = 0; y < m_PatternHeight; ++y)
        {
            int[] ay = new int[3]
            {
                (y + m_PatternHeight - 1) % m_PatternHeight,
                y,
                (y + 1) % m_PatternHeight
            };

            for (int x = 0; x < m_PatternWidth; ++x)
            {
                int[] ax = new int[3]
                {
                    (x + m_PatternWidth - 1) % m_PatternWidth,
                    x,
                    (x + 1) % m_PatternWidth
                };

                Vector2 center = GetPixelPos(x, y);
                float minDist = 1000.0f;
                Vector2 minPos = new Vector2(0.0f, 0.0f);
                for (int iy = 0; iy < 3; iy++)
                    for (int ix = 0; ix < 3; ix++)
                    {
                        if ((ix != 1) || (iy != 1))
                        {
                            Vector2 pos = GetPixelPos(ax[ix], ay[iy]);
                            float dist = Vector2.Distance(center, pos);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                minPos = pos;
                            }
                        }
                    }

                if (minDist < 1000.0f)
                {
                    Color p = m_Colors[y * m_PatternWidth + x];
                    p.r += (1.1f / 255.0f) * (minPos.x < center.x ? 1.0f : -1.0f);
                    p.g += (1.1f / 255.0f) * (minPos.y < center.y ? 1.0f : -1.0f);
                    p.r = Mathf.Clamp01(p.r);
                    p.g = Mathf.Clamp01(p.g);
                    m_Colors[y * m_PatternWidth + x] = p;
                }
            }
        }
    }

    void GenerateRandomPattern()
    {
        for (int y = 0; y < m_PatternHeight; ++y)
        {
            if ((m_PatternType != PatternType.Rows || y % 2 == 0))
            {
                for (int x = 0; x < m_PatternWidth; ++x)
                {
                    if ((m_PatternType != PatternType.Columns || x % 2 == 0) &&
                        (m_PatternType != PatternType.Border || x == 0 || y == 0 || x == m_PatternWidth - 1 || y == m_PatternHeight - 1) &&
                        (m_PatternType != PatternType.Alternating || (x + y) % 2 == 0))
                    {
                        byte threshold = (byte)UnityEngine.Random.Range(1, 255);
                        byte jitterX = (byte)((m_JitterX * UnityEngine.Random.Range(-1.0f, 1.0f) + 1.0f) * 127.5f);
                        byte jitterY = (byte)((m_JitterY * UnityEngine.Random.Range(-1.0f, 1.0f) + 1.0f) * 127.5f);

                        m_Colors[y * m_PatternWidth + x] = new Color32(jitterX, jitterY, threshold, 1);
                    }
                    else
                    {
                        m_Colors[y * m_PatternWidth + x] = new Color(0, 0, 0, 0);
                    }
                }
            }
            else
            {
                for (int x = 0; x < m_PatternWidth; ++x)
                {
                    m_Colors[y * m_PatternWidth + x] = new Color(0, 0, 0, 0);
                }
            }
        }
    }

    int m_SampleCount = 256;

    int m_PatternWidth = 16;
    int m_PatternHeight = 16;

    int m_PlacementSize = 8;
    int m_PlacementSpacing = 16;

    float m_JitterX = 0.0f;
    float m_JitterY = 0.0f;

    PatternType m_PatternType = PatternType.Fill;
    int m_Threshold = 256;

    Texture2D m_Placement;
    Texture2D m_Background;

    Color[] m_Colors;
}
