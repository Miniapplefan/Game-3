using System;
using UnityEngine;

namespace CameraProjectionRenderingToolkit
{
    [AddComponentMenu("Image Effects/CPRT Split Pannini")]
    [RequireComponent(typeof(Camera))]
    public sealed class CPRTSplitPannini : MonoBehaviour
    {
        public enum FOVSettingType
        {
            Vertical,
            Horizontal,
            Diagonal
        }

        private const string SplitShaderName = "Hidden/CPRT/SplitPannini";
        private const float ObserverNearClip = 0.0625f;
        private const float ObserverFarClip = 64.0f;
        private const int MinProjectionPrecision = 4;
        private const int MaxProjectionPrecision = 64;

        [Header("Wide Projection")]
        [Range(1.0f, 179.0f)] public float fieldOfView = 160.0f;
        public FOVSettingType fieldOfViewSetting = FOVSettingType.Vertical;
        [Range(0.0f, 1.25f)] public float intensity = 0.7f;
        public bool adaptivePannini = true;
        public bool isAdaptiveAutomatic;
        [Range(0.1f, 16.0f)] public float adaptivePower = 9.0f;
        [Range(0.0f, 1.0f)] public float adaptiveTolerance = 1.0f;

        [Header("Center Render")]
        [Range(20.0f, 140.0f)] public float centerFieldOfView = 70.0f;
        public FOVSettingType centerFieldOfViewSetting = FOVSettingType.Vertical;
        [Range(0.25f, 1.0f)] public float peripheralRenderScale = 0.55f;
        [Range(0.5f, 2.0f)] public float centerRenderScale = 1.0f;
        [Range(0.01f, 0.35f)] public float centerBlendMargin = 0.12f;
        public bool useMainCameraCullingMaskForCenter = true;
        public LayerMask centerCullingMask = ~0;

        [Header("Stylization")]
        [Range(0.0f, 4.0f)] public float peripheralBlur = 1.4f;
        [Range(0.0f, 2.0f)] public float centerSharpen = 0.35f;
        [Range(0.0f, 1.0f)] public float peripheralDesaturation = 0.08f;
        [Range(0.0f, 1.0f)] public float peripheralContrast = 0.05f;
        public Color peripheralTint = Color.white;

        [Header("Quality")]
        [Range(MinProjectionPrecision, MaxProjectionPrecision)] public int projectionPrecision = 24;
        public bool projectionWireframe;
        public Shader splitShader;

        private Camera hostCamera;
        private Camera peripheralCamera;
        private Camera centerCamera;
        private Material splitMaterial;
        private Mesh projectionMesh;
        private RenderTexture peripheralTexture;
        private RenderTexture centerTexture;
        private float wideFieldOfViewY = 90.0f;
        private float centerFieldOfViewYResolved = 70.0f;
        private float widthAperture;
        private float observerFov = 90.0f;
        private int lastViewportWidth;
        private int lastViewportHeight;
        private int lastPeripheralWidth;
        private int lastPeripheralHeight;
        private int lastCenterWidth;
        private int lastCenterHeight;
        private float lastMeshAspect = -1.0f;
        private int lastMeshPrecision = -1;
        private bool lastMeshAdaptive;
        private DateTime lastPreCull;
        private int savedHostCullingMask = -1;
        private CameraClearFlags savedHostClearFlags;
        private bool hostRenderSuppressed;

        private float AdaptivePanniniAngle => Mathf.Asin(transform.forward.normalized.y);

        private void Start()
        {
            lastPreCull = DateTime.Now;
            EnsureSetup();
        }

        private void OnEnable()
        {
            lastPreCull = DateTime.Now;
            EnsureSetup();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnValidate()
        {
            fieldOfView = Mathf.Clamp(fieldOfView, 1.0f, 179.0f);
            centerFieldOfView = Mathf.Clamp(centerFieldOfView, 1.0f, Mathf.Min(fieldOfView, 179.0f));
            peripheralRenderScale = Mathf.Clamp(peripheralRenderScale, 0.25f, 1.0f);
            centerRenderScale = Mathf.Clamp(centerRenderScale, 0.5f, 2.0f);
            centerBlendMargin = Mathf.Clamp(centerBlendMargin, 0.01f, 0.35f);
            projectionPrecision = Mathf.Clamp(projectionPrecision, MinProjectionPrecision, MaxProjectionPrecision);
            peripheralBlur = Mathf.Max(0.0f, peripheralBlur);
            centerSharpen = Mathf.Max(0.0f, centerSharpen);
            peripheralDesaturation = Mathf.Clamp01(peripheralDesaturation);
            peripheralContrast = Mathf.Clamp01(peripheralContrast);
            adaptiveTolerance = Mathf.Clamp01(adaptiveTolerance);
            adaptivePower = Mathf.Max(0.1f, adaptivePower);

            if (isActiveAndEnabled)
            {
                EnsureSetup();
            }
        }

        private void EnsureSetup()
        {
            hostCamera = GetComponent<Camera>();
            EnsureMaterial();
            EnsurePeripheralCamera();
            EnsureCenterCamera();
        }

        private void EnsureMaterial()
        {
            if (splitShader == null)
            {
                splitShader = Shader.Find(SplitShaderName);
            }

            if (splitShader == null || !splitShader.isSupported)
            {
                return;
            }

            if (splitMaterial == null || splitMaterial.shader != splitShader)
            {
                if (splitMaterial != null)
                {
                    DestroyImmediate(splitMaterial);
                }

                splitMaterial = new Material(splitShader)
                {
                    hideFlags = HideFlags.DontSave
                };
            }
        }

        private void EnsurePeripheralCamera()
        {
            if (peripheralCamera != null)
            {
                return;
            }

            peripheralCamera = EnsureHelperCamera("__CPRT Split Peripheral Camera");
        }

        private void EnsureCenterCamera()
        {
            if (centerCamera != null)
            {
                return;
            }

            centerCamera = EnsureHelperCamera("__CPRT Split Center Camera");
        }

        private Camera EnsureHelperCamera(string helperName)
        {
            Transform child = transform.Find(helperName);
            GameObject cameraObject;
            if (child != null)
            {
                cameraObject = child.gameObject;
            }
            else
            {
                cameraObject = new GameObject(helperName);
                cameraObject.transform.SetParent(transform, false);
                cameraObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            }

            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            cameraObject.transform.localScale = Vector3.one;

            Camera helperCamera = cameraObject.GetComponent<Camera>();
            if (helperCamera == null)
            {
                helperCamera = cameraObject.AddComponent<Camera>();
            }

            helperCamera.enabled = false;
            helperCamera.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;

            AudioListener listener = cameraObject.GetComponent<AudioListener>();
            if (listener != null)
            {
                DestroyImmediate(listener);
            }

            return helperCamera;
        }

        private void Cleanup()
        {
            RestoreHostRenderState();

            if (hostCamera != null)
            {
                hostCamera.targetTexture = null;
            }

            ReleaseRenderTexture(ref peripheralTexture);
            ReleaseRenderTexture(ref centerTexture);

            if (projectionMesh != null)
            {
                DestroyImmediate(projectionMesh);
                projectionMesh = null;
            }

            if (splitMaterial != null)
            {
                DestroyImmediate(splitMaterial);
                splitMaterial = null;
            }

            if (peripheralCamera != null)
            {
                DestroyImmediate(peripheralCamera.gameObject);
                peripheralCamera = null;
            }

            if (centerCamera != null)
            {
                DestroyImmediate(centerCamera.gameObject);
                centerCamera = null;
            }
        }

        private void OnPreCull()
        {
            EnsureSetup();

            if (!IsReady() || !ShouldRenderForCamera())
            {
                RestoreHostRenderState();
                return;
            }

            RestoreHostRenderState();

            GetFinalViewportSize(out int viewportWidth, out int viewportHeight);
            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                return;
            }

            float aspect = viewportWidth / (float)viewportHeight;
            wideFieldOfViewY = ResolveFieldOfView(fieldOfView, fieldOfViewSetting, aspect);
            centerFieldOfViewYResolved = ResolveFieldOfView(centerFieldOfView, centerFieldOfViewSetting, aspect);
            widthAperture = CPRTToolkit.GetFovX(wideFieldOfViewY * Mathf.Deg2Rad, aspect);

            EnsureProjectionMesh(aspect);
            EnsureRenderTargets(viewportWidth, viewportHeight);

            SyncPeripheralCamera(aspect);
            SyncCenterCamera(aspect);
            if (peripheralTexture != null)
            {
                peripheralCamera.Render();
            }
            if (centerTexture != null)
            {
                centerCamera.Render();
            }

            hostCamera.fieldOfView = wideFieldOfViewY;
            SuppressHostSceneRender();
            lastPreCull = DateTime.Now;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            hostCamera.targetTexture = null;

            if (!IsReady() || !ShouldRenderForCamera() || peripheralTexture == null || centerTexture == null || projectionMesh == null)
            {
                Graphics.Blit(source, destination);
                RestoreHostRenderState();
                return;
            }

            bool drawInTexture = destination != null;
            float aspect = peripheralTexture.width / (float)peripheralTexture.height;
            BuildProjectionMatrices(aspect, drawInTexture, out Matrix4x4 observerViewProj, out Matrix4x4 widePainterViewProj, out Matrix4x4 centerPainterViewProj);

            splitMaterial.SetTexture("_PeripheralTex", peripheralTexture);
            splitMaterial.SetTexture("_CenterTex", centerTexture);
            splitMaterial.SetVector("_PeripheralTexelSize", new Vector4(1.0f / peripheralTexture.width, 1.0f / peripheralTexture.height, peripheralTexture.width, peripheralTexture.height));
            splitMaterial.SetVector("_CenterTexelSize", new Vector4(1.0f / centerTexture.width, 1.0f / centerTexture.height, centerTexture.width, centerTexture.height));
            splitMaterial.SetMatrix("ObserverViewProj", observerViewProj);
            splitMaterial.SetMatrix("PeripheralPainterViewProj", widePainterViewProj);
            splitMaterial.SetMatrix("CenterPainterViewProj", centerPainterViewProj);
            splitMaterial.SetFloat("CenterBlendMargin", centerBlendMargin);
            splitMaterial.SetFloat("PeripheralBlur", peripheralBlur);
            splitMaterial.SetFloat("CenterSharpen", centerSharpen);
            splitMaterial.SetFloat("PeripheralDesaturation", peripheralDesaturation);
            splitMaterial.SetFloat("PeripheralContrast", peripheralContrast);
            splitMaterial.SetColor("PeripheralTint", peripheralTint);

            GL.PushMatrix();
            if (destination != null)
            {
                Graphics.SetRenderTarget(destination);
            }
            else
            {
                Graphics.SetRenderTarget(null as RenderTexture);
            }
            GL.Viewport(new Rect(0.0f, 0.0f, drawInTexture ? destination.width : Screen.width, drawInTexture ? destination.height : Screen.height));
            GL.Clear(true, true, hostCamera.backgroundColor);

            if (projectionWireframe)
            {
                GL.wireframe = true;
            }

            splitMaterial.SetPass(0);
            GL.modelview = Matrix4x4.identity;
            Graphics.DrawMeshNow(projectionMesh, Matrix4x4.identity, 0);

            if (projectionWireframe)
            {
                GL.wireframe = false;
            }

            GL.PopMatrix();
            RestoreHostRenderState();
        }

        private bool IsReady()
        {
            return enabled
                && isActiveAndEnabled
                && hostCamera != null
                && peripheralCamera != null
                && centerCamera != null
                && splitMaterial != null
                && splitMaterial.shader != null;
        }

        private bool ShouldRenderForCamera()
        {
            return hostCamera != null && hostCamera.cameraType == CameraType.Game;
        }

        private void GetFinalViewportSize(out int width, out int height)
        {
            width = Mathf.Max(1, hostCamera.pixelWidth);
            height = Mathf.Max(1, hostCamera.pixelHeight);
        }

        private float ResolveFieldOfView(float configuredFov, FOVSettingType settingType, float aspect)
        {
            switch (settingType)
            {
                case FOVSettingType.Horizontal:
                    return CPRTToolkit.GetFovY(configuredFov * Mathf.Deg2Rad, aspect) * Mathf.Rad2Deg;
                case FOVSettingType.Diagonal:
                    return CPRTToolkit.GetFovYFromDiagAspect(configuredFov * Mathf.Deg2Rad, aspect) * Mathf.Rad2Deg;
                default:
                    return configuredFov;
            }
        }

        private void EnsureRenderTargets(int viewportWidth, int viewportHeight)
        {
            int peripheralWidth = Mathf.Max(16, Mathf.RoundToInt(viewportWidth * peripheralRenderScale));
            int peripheralHeight = Mathf.Max(16, Mathf.RoundToInt(viewportHeight * peripheralRenderScale));
            int centerWidth = Mathf.Max(16, Mathf.RoundToInt(viewportWidth * centerRenderScale));
            int centerHeight = Mathf.Max(16, Mathf.RoundToInt(viewportHeight * centerRenderScale));

            lastViewportWidth = viewportWidth;
            lastViewportHeight = viewportHeight;

            bool hdr = hostCamera.allowHDR;
            RenderTextureFormat colorFormat = hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

            if (peripheralTexture == null || lastPeripheralWidth != peripheralWidth || lastPeripheralHeight != peripheralHeight)
            {
                ReleaseRenderTexture(ref peripheralTexture);
                peripheralTexture = CreateRenderTexture(peripheralWidth, peripheralHeight, colorFormat, "_CPRT Peripheral");
                lastPeripheralWidth = peripheralWidth;
                lastPeripheralHeight = peripheralHeight;
            }

            if (centerTexture == null || lastCenterWidth != centerWidth || lastCenterHeight != centerHeight)
            {
                ReleaseRenderTexture(ref centerTexture);
                centerTexture = CreateRenderTexture(centerWidth, centerHeight, colorFormat, "_CPRT Center");
                lastCenterWidth = centerWidth;
                lastCenterHeight = centerHeight;
            }
        }

        private RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat format, string debugName)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, format, 16)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
            };

            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = debugName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.Create();
            return texture;
        }

        private void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            if (texture.IsCreated())
            {
                texture.Release();
            }

            DestroyImmediate(texture);
            texture = null;
        }

        private void SyncPeripheralCamera(float aspect)
        {
            peripheralCamera.CopyFrom(hostCamera);
            peripheralCamera.enabled = false;
            peripheralCamera.targetTexture = peripheralTexture;
            peripheralCamera.fieldOfView = wideFieldOfViewY;
            peripheralCamera.aspect = aspect;
            peripheralCamera.allowMSAA = false;
            peripheralCamera.useOcclusionCulling = hostCamera.useOcclusionCulling;
            peripheralCamera.depth = hostCamera.depth - 2.0f;
        }

        private void SyncCenterCamera(float aspect)
        {
            centerCamera.CopyFrom(hostCamera);
            centerCamera.enabled = false;
            centerCamera.targetTexture = centerTexture;
            centerCamera.fieldOfView = centerFieldOfViewYResolved;
            centerCamera.aspect = aspect;
            centerCamera.cullingMask = useMainCameraCullingMaskForCenter ? hostCamera.cullingMask : centerCullingMask;
            centerCamera.allowMSAA = false;
            centerCamera.useOcclusionCulling = hostCamera.useOcclusionCulling;
            centerCamera.depth = hostCamera.depth - 1.0f;
        }

        private void SuppressHostSceneRender()
        {
            if (hostRenderSuppressed)
            {
                return;
            }

            savedHostCullingMask = hostCamera.cullingMask;
            savedHostClearFlags = hostCamera.clearFlags;
            hostCamera.cullingMask = 0;
            hostCamera.clearFlags = CameraClearFlags.SolidColor;
            hostRenderSuppressed = true;
        }

        private void RestoreHostRenderState()
        {
            if (!hostRenderSuppressed || hostCamera == null)
            {
                return;
            }

            hostCamera.cullingMask = savedHostCullingMask;
            hostCamera.clearFlags = savedHostClearFlags;
            savedHostCullingMask = -1;
            hostRenderSuppressed = false;
        }

        private void EnsureProjectionMesh(float aspect)
        {
            if (projectionMesh == null)
            {
                projectionMesh = new Mesh
                {
                    name = "CPRT Split Pannini Mesh",
                    hideFlags = HideFlags.DontSave
                };
            }

            if (Mathf.Approximately(lastMeshAspect, aspect)
                && lastMeshPrecision == projectionPrecision
                && lastMeshAdaptive == adaptivePannini)
            {
                return;
            }

            lastMeshAspect = aspect;
            lastMeshPrecision = projectionPrecision;
            lastMeshAdaptive = adaptivePannini;

            const float topDown = 32.0f;
            float maxAperture = adaptivePannini ? widthAperture * 1.5f : widthAperture;
            float startAngle = -maxAperture * 0.5f;
            float angleFactor = maxAperture / projectionPrecision;
            int quadId;

            projectionMesh.Clear();

            Vector3[] borderVertices =
            {
                new Vector3(-1.0f, -topDown, -1.0f),
                new Vector3(1.0f, -topDown, -1.0f),
                new Vector3(-1.0f, -topDown, 2.0f),
                new Vector3(1.0f, -topDown, 2.0f),
                new Vector3(-1.0f, topDown, -1.0f),
                new Vector3(1.0f, topDown, -1.0f),
                new Vector3(-1.0f, topDown, 2.0f),
                new Vector3(1.0f, topDown, 2.0f)
            };

            int[] borderTriangles =
            {
                0, 1, 2, 1, 2, 3,
                4, 5, 6, 5, 6, 7,
                0, 1, 4, 4, 1, 5,
                2, 0, 4, 2, 4, 6,
                1, 3, 5, 5, 3, 7
            };

            var vertices = new System.Collections.Generic.List<Vector3>((projectionPrecision + 1) * 2 + borderVertices.Length);
            var triangles = new System.Collections.Generic.List<int>(projectionPrecision * 6 + borderTriangles.Length);
            vertices.AddRange(borderVertices);
            triangles.AddRange(borderTriangles);
            quadId = borderVertices.Length;

            for (int i = 0; i <= projectionPrecision; i++)
            {
                float angle = startAngle + i * angleFactor;
                vertices.Add(new Vector3(Mathf.Sin(angle), topDown, Mathf.Cos(angle)));
                vertices.Add(new Vector3(Mathf.Sin(angle), -topDown, Mathf.Cos(angle)));

                if (i < projectionPrecision)
                {
                    triangles.AddRange(new[] { quadId, quadId + 1, quadId + 2, quadId + 2, quadId + 1, quadId + 3 });
                    quadId += 2;
                }
            }

            projectionMesh.SetVertices(vertices);
            projectionMesh.SetTriangles(triangles, 0, false);
            projectionMesh.RecalculateBounds();
        }

        private void BuildProjectionMatrices(float aspect, bool drawInTexture, out Matrix4x4 observerViewProj, out Matrix4x4 widePainterViewProj, out Matrix4x4 centerPainterViewProj)
        {
            Matrix4x4 widePainterProjection = Matrix4x4.Perspective(wideFieldOfViewY, aspect, hostCamera.nearClipPlane, hostCamera.farClipPlane);
            Matrix4x4 centerPainterProjection = Matrix4x4.Perspective(centerFieldOfViewYResolved, aspect, hostCamera.nearClipPlane, hostCamera.farClipPlane);
            float intensityFactor = GetIntensityFactor();
            float offz = intensityFactor * intensity;

            if (adaptivePannini)
            {
                float angle = AdaptivePanniniAngle;
                Vector3 paintAt = new Vector3(0.0f, Mathf.Sin(angle), Mathf.Cos(angle));
                Matrix4x4 painterView = CPRTToolkit.LookAtRH(Vector3.zero, paintAt, Vector3.up);

                widePainterViewProj = widePainterProjection * painterView;
                centerPainterViewProj = centerPainterProjection * painterView;

                Vector3 observerAt = new Vector3(0.0f, Mathf.Abs(angle) < Mathf.PI * 0.5f ? Mathf.Tan(angle) : Mathf.Sign(angle) * 16384.0f, 1.0f);
                observerViewProj = CPRTToolkit.LookAtRH(new Vector3(0.0f, 0.0f, -offz), observerAt, Vector3.up);
                float centerScaling = GetStereoProjectionCenterScaling(offz, aspect);
                observerFov = Mathf.Rad2Deg * CPRTToolkit.ComputeAdaptivePaniniProjFOV(widthAperture, aspect, -offz, intensityFactor, centerScaling);
            }
            else
            {
                Matrix4x4 painterView = CPRTToolkit.LookAtRH();
                widePainterViewProj = widePainterProjection * painterView;
                centerPainterViewProj = centerPainterProjection * painterView;

                observerViewProj = CPRTToolkit.LookAtRH(new Vector3(0.0f, 0.0f, -offz), Vector3.forward, Vector3.up);
                observerFov = Mathf.Rad2Deg * CPRTToolkit.ComputePaniniProjFOV(widthAperture, aspect, -offz);
            }

            Matrix4x4 observerProjection = GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(observerFov, aspect, ObserverNearClip, ObserverFarClip), drawInTexture);
            observerViewProj = observerProjection * observerViewProj;
        }

        private float GetIntensityFactor()
        {
            if (!adaptivePannini)
            {
                return 1.0f;
            }

            float minY = (1.0f - adaptiveTolerance) * Mathf.Cos(Mathf.Deg2Rad * 0.5f * (180.0f - wideFieldOfViewY));
            float intensityFactor = (Mathf.Abs(transform.up.y) - minY) / Mathf.Max(0.0001f, 1.0f - minY);

            if (isAdaptiveAutomatic)
            {
                intensityFactor = intensityFactor <= 0.0f ? 0.0f : (0.5f - 0.5f * Mathf.Cos(2.0f * Mathf.Asin(Mathf.Clamp01(intensityFactor))));
            }
            else
            {
                intensityFactor = intensityFactor <= 0.0f ? 0.0f : Mathf.Pow(intensityFactor, adaptivePower);
            }

            return Mathf.Clamp01(intensityFactor);
        }

        private float GetStereoProjectionCenterScaling(float offz, float aspect)
        {
            const float delta = 0.01f;
            Ray projectedRay = GetPanniniViewportRay(new Vector2(0.5f + delta, 0.5f + delta), offz, aspect);
            Ray projectedOriginRay = GetPanniniViewportRay(new Vector2(0.5f, 0.5f), offz, aspect);
            Ray linearRay = hostCamera.ViewportPointToRay(new Vector3(0.5f + delta, 0.5f + delta, 1.0f));
            Ray linearOriginRay = hostCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 1.0f));

            float projectedAngle = Vector3.Angle(projectedRay.direction, projectedOriginRay.direction);
            if (projectedAngle <= Mathf.Epsilon)
            {
                return 1.0f;
            }

            return Vector3.Angle(linearRay.direction, linearOriginRay.direction) / projectedAngle;
        }

        private Ray GetPanniniViewportRay(Vector2 viewportPosition, float offz, float aspect)
        {
            if (offz <= 0.0f)
            {
                return hostCamera.ViewportPointToRay(viewportPosition);
            }

            Vector3 direction = new Vector3(viewportPosition.x * 2.0f - 1.0f, viewportPosition.y * 2.0f - 1.0f, 1.0f);
            Matrix4x4 viewProjection = CPRTToolkit.LookAtRH(new Vector3(0.0f, 0.0f, -offz), Vector3.forward, Vector3.up);
            float localObserverFov = Mathf.Rad2Deg * CPRTToolkit.ComputePaniniProjFOV(widthAperture, aspect, -offz);
            viewProjection = Matrix4x4.Perspective(localObserverFov, aspect, ObserverNearClip, ObserverFarClip) * viewProjection;

            direction = viewProjection.inverse.MultiplyPoint(direction);
            direction.z += offz;

            Vector2 flattened = new Vector2(direction.x, direction.z).normalized;
            flattened *= flattened.y * offz + Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - CPRTToolkit.Sq(flattened.x * offz)));

            direction = new Vector3(flattened.x, direction.y * flattened.y / Mathf.Max(0.0001f, direction.z), flattened.y - offz);
            direction.x = -direction.x;
            direction = transform.TransformDirection(direction).normalized;

            return new Ray(transform.position, direction);
        }
    }
}
