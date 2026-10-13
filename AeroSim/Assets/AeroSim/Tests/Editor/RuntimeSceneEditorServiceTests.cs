using NUnit.Framework;
using UnityEngine;

namespace AeroSim.Runtime.SceneEditor.Tests
{
    public sealed class RuntimeSceneEditorServiceTests
    {
        private GameObject host;
        private RuntimeSceneEditorService service;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("RuntimeSceneEditorServiceTests");
            service = host.AddComponent<RuntimeSceneEditorService>();
            service.EnterSandbox();
        }

        [TearDown]
        public void TearDown()
        {
            if (service != null && service.IsSandboxActive) service.ExitSandbox(true);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void SpawnTransformDeleteUndoRedo_RemainsDraftOnly()
        {
            var item = service.Spawn(SandboxPrimitive.Cube);
            var materialLifetime = item.GetComponent<DraftMaterialLifetime>();
            Assert.That(item.IsDraft, Is.True);
            Assert.That(materialLifetime.OwnedMaterialCount, Is.GreaterThan(0));
            Assert.That(item.Status, Is.EqualTo(RuntimeSceneObject.DraftStatus));
            Assert.That(item.RuntimeId, Does.StartWith("draft-"));
            Assert.That(service.Hierarchy().Count, Is.EqualTo(1));

            service.SetSelectedTransform(new Vector3(2, 3, 4), new Vector3(0, 45, 0), new Vector3(2, 2, 2));
            Assert.That(item.transform.position, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(service.Undo(), Is.True);
            Assert.That(item.transform.position, Is.Not.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(service.Redo(), Is.True);
            Assert.That(item.transform.position, Is.EqualTo(new Vector3(2, 3, 4)));

            service.DeleteSelected();
            Assert.That(service.Hierarchy(), Is.Empty);
            Assert.That(materialLifetime.OwnedMaterialCount, Is.Zero);
            Assert.That(service.Undo(), Is.True);
            Assert.That(service.Hierarchy().Count, Is.EqualTo(1));
            Assert.That(materialLifetime.OwnedMaterialCount, Is.GreaterThan(0));
            Assert.That(service.Redo(), Is.True);
            Assert.That(materialLifetime.OwnedMaterialCount, Is.Zero);
            Assert.That(service.Undo(), Is.True);
            Assert.That(materialLifetime.OwnedMaterialCount, Is.GreaterThan(0));
        }

        [Test]
        public void ClearAndExitAreRollbackBoundaries()
        {
            var sphere = service.Spawn(SandboxPrimitive.Sphere);
            var rock = service.Spawn(SandboxPrimitive.RockLike);
            var sphereMaterials = sphere.GetComponent<DraftMaterialLifetime>();
            var rockMaterials = rock.GetComponent<DraftMaterialLifetime>();
            Assert.That(service.ClearDrafts(), Is.True);
            Assert.That(service.Hierarchy(), Is.Empty);
            Assert.That(sphereMaterials.OwnedMaterialCount, Is.Zero);
            Assert.That(rockMaterials.OwnedMaterialCount, Is.Zero);
            Assert.That(service.Undo(), Is.True);
            Assert.That(service.Hierarchy().Count, Is.EqualTo(2));
            Assert.That(sphereMaterials.OwnedMaterialCount, Is.GreaterThan(0));
            Assert.That(rockMaterials.OwnedMaterialCount, Is.GreaterThan(0));

            service.ExitSandbox(true);
            Assert.That(service.IsSandboxActive, Is.False);
            Assert.That(service.Hierarchy(), Is.Empty);
            Assert.That(sphere == null, Is.True);
            Assert.That(rock == null, Is.True);
            Assert.Throws<System.InvalidOperationException>(() => service.Spawn(SandboxPrimitive.Cube));
        }

        [Test]
        public void TransformRejectsNonFiniteBoundaryAndUnsafeScale()
        {
            var item = service.Spawn(SandboxPrimitive.Cylinder);
            var original = item.transform.position;
            int historyBefore = service.UndoCount;
            Assert.Throws<System.ArgumentException>(() =>
                service.SetSelectedTransform(new Vector3(float.NaN, 0, 0), Vector3.zero, Vector3.one));
            Assert.Throws<System.ArgumentException>(() =>
                service.SetSelectedTransform(new Vector3(RuntimeSceneEditorService.BoundaryRadius + .01f, 0, 0), Vector3.zero, Vector3.one));
            Assert.Throws<System.ArgumentException>(() =>
                service.SetSelectedTransform(new Vector3(0, RuntimeSceneEditorService.BoundaryMaxY + .01f, 0), Vector3.zero, Vector3.one));
            Assert.Throws<System.ArgumentException>(() =>
                service.SetSelectedTransform(Vector3.zero, Vector3.zero, new Vector3(.01f, 1, 1)));
            Assert.Throws<System.ArgumentException>(() =>
                service.SetSelectedTransform(Vector3.zero, Vector3.zero, new Vector3(51, 1, 1)));
            Assert.Throws<System.ArgumentException>(() =>
                service.SetSelectedTransform(Vector3.zero, Vector3.zero, new Vector3(.02f, 2.1f, 1)));
            Assert.That(item.transform.position, Is.EqualTo(original));
            Assert.That(service.UndoCount, Is.EqualTo(historyBefore));

            Assert.DoesNotThrow(() => service.SetSelectedTransform(
                new Vector3(RuntimeSceneEditorService.BoundaryRadius, RuntimeSceneEditorService.BoundaryMinY, -RuntimeSceneEditorService.BoundaryRadius),
                new Vector3(0, 30, 0), new Vector3(.02f, .02f, .02f)));
            Assert.DoesNotThrow(() => service.SetSelectedTransform(
                new Vector3(-RuntimeSceneEditorService.BoundaryRadius, RuntimeSceneEditorService.BoundaryMaxY, RuntimeSceneEditorService.BoundaryRadius),
                new Vector3(0, 30, 0), new Vector3(.5f, 50f, .5f)));
        }
    }
}
