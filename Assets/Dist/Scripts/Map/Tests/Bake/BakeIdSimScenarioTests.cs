// ============================================================
// BakeIdSimScenarioTests — BakeId.Sim (BakeIdPlayground 씬 Host SSOT)
// ============================================================
#if UNITY_EDITOR
using IsoTilemap;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoTilemap.Tests
{
    /// <summary>
    /// 씬에 저장된 simTiles/probes/rules로 bake ID 규칙을 검증한다.
    /// 배치·규칙 변경은 BakeIdPlaygroundHost 편집 후 씬 저장.
    /// </summary>
    public sealed class BakeIdSimScenarioTests
    {
        [Test]
        [Category("BakeId.Sim")]
        public void SceneHost_SimRules_Pass()
        {
            Scene prev = EditorSceneManager.GetActiveScene();
            string prevPath = prev.path;

            EditorSceneManager.OpenScene(
                BakeIdPlaygroundHost.ScenePath,
                OpenSceneMode.Single);

            try
            {
                BakeIdPlaygroundHost host = Object.FindObjectOfType<BakeIdPlaygroundHost>();
                Assert.IsNotNull(host, $"BakeIdPlaygroundHost missing in {BakeIdPlaygroundHost.ScenePath}");
                Assert.Greater(
                    host.SimTiles.Count,
                    0,
                    "simTiles empty — open BakeIdPlayground, Import From Seed Layout, save scene");

                BakeIdSimRunner.Result result = BakeIdSimRunner.Run(
                    host.SimTiles,
                    host.SimProbes,
                    host.SimRules,
                    host.SimSteps);

                if (!result.Ok)
                {
                    Assert.Fail(
                        "BakeId.Sim rules failed:\n- " +
                        string.Join("\n- ", result.Failures));
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(prevPath) &&
                    prevPath != BakeIdPlaygroundHost.ScenePath &&
                    System.IO.File.Exists(prevPath))
                {
                    EditorSceneManager.OpenScene(prevPath, OpenSceneMode.Single);
                }
            }
        }
    }
}
#endif
