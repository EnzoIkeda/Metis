using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

public class CityBloomControllerTests
{
    private const string TestKey = "Metis.Tests.Bloom";

    private GameObject _cameraObject;
    private GameObject _controllerObject;

    private static void SetStatic(string name, object value)
    {
        typeof(BloomSettings).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    }

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(TestKey);
        SetStatic("_prefsKeyOverride", TestKey);
        SetStatic("_enabled", null);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_controllerObject);
        Object.Destroy(_cameraObject);
        PlayerPrefs.DeleteKey(TestKey);
        SetStatic("_enabled", null);
        SetStatic("_prefsKeyOverride", null);
    }

    private (Camera camera, Volume volume) Build()
    {
        _cameraObject = new GameObject("Camera");
        var camera = _cameraObject.AddComponent<Camera>();
        _cameraObject.AddComponent<UniversalAdditionalCameraData>();

        _controllerObject = new GameObject("Bloom");
        _controllerObject.SetActive(false);
        var volume = _controllerObject.AddComponent<Volume>();
        var controller = _controllerObject.AddComponent<CityBloomController>();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(CityBloomController).GetField("_camera", flags).SetValue(controller, camera);
        typeof(CityBloomController).GetField("_volume", flags).SetValue(controller, volume);
        _controllerObject.SetActive(true);
        return (camera, volume);
    }

    [UnityTest]
    public IEnumerator StartsOff_AndFollowsTheSetting()
    {
        var (camera, volume) = Build();
        yield return null;

        var data = camera.GetUniversalAdditionalCameraData();
        Assert.That(volume.enabled, Is.False);
        Assert.That(data.renderPostProcessing, Is.False);

        BloomSettings.Enabled = true;
        Assert.That(volume.enabled, Is.True);
        Assert.That(data.renderPostProcessing, Is.True);

        BloomSettings.Enabled = false;
        Assert.That(volume.enabled, Is.False);
        Assert.That(data.renderPostProcessing, Is.False);
    }

    [UnityTest]
    public IEnumerator Disabled_StopsListening()
    {
        var (camera, volume) = Build();
        yield return null;

        _controllerObject.SetActive(false);
        BloomSettings.Enabled = true;

        Assert.That(volume.enabled, Is.False);
        Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.False);
    }
}
