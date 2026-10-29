using UnityEngine;

namespace Lizards
{
    /// <summary>Creación de materiales URP sencillos (sombreado suave, sin texturas).</summary>
    public static class LizardMaterials
    {
        static Shader lit, particle;
        static Material particleMat;

        static Shader LitShader => lit ? lit : (lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        static Shader ParticleShader => particle ? particle : (particle = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));

        public static Material Lit(Color c, float smoothness) => Lit(c, smoothness, Color.black);

        public static Material Lit(Color c, float smoothness, Color emission)
        {
            var m = new Material(LitShader);
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        public static Material Transparent(Color c, float smoothness = 0.9f)
        {
            var m = Lit(c, smoothness);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        /// <summary>Material blanco con color de vértice (líneas, estelas, partículas).</summary>
        public static Material Particle()
        {
            if (particleMat) return particleMat;
            particleMat = new Material(ParticleShader);
            particleMat.SetColor("_BaseColor", Color.white);
            return particleMat;
        }
    }
}
