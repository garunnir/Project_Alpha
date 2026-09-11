// ============================================================
// BakeIdSimScenarioTests — BakeId.Sim (seed defaults + 씬 Host SSOT)
// ============================================================
#if UNITY_EDITOR
using System.Collections.Generic;
using IsoTilemap;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoTilemap.Tests
{
    /// <summary>
    /// Seed defaults는 코드 SSOT. 씬 Host 규칙은 Import From Seed Layout 후 저장본.
    /// </summary>
    public sealed class BakeIdSimScenarioTests
    {
        [Test]
        [Category("BakeId.Sim")]
        public void SeedDefaults_SimRules_Pass()
        {
            var tiles = new List<BakeIdSimTileEntry>();
            var probes = new List<BakeIdSimProbe>();
            var rules = new List<BakeIdSimRule>();
            BakeIdSimDefaults.FillFromUnitMasterPlayground(tiles, probes, rules);

            Assert.Greater(tiles.Count, 0, "seed tiles empty");
            Assert.Greater(rules.Count, 0, "seed rules empty");

            BakeIdSimRunner.Result result = BakeIdSimRunner.Run(tiles, probes, rules, null);
            if (!result.Ok)
            {
                Assert.Fail(
                    "BakeId.Sim seed rules failed:\n- " +
                    string.Join("\n- ", result.Failures));
            }
        }

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
                        "BakeId.Sim scene Host rules failed (re-Import Seed Layout if stale):\n- " +
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
