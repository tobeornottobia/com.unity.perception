using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Perception.GroundTruth.Consumers;
using UnityEngine.Perception.GroundTruth.DataModel;
using UnityEngine.Perception.Settings;

namespace GroundTruthTests
{
    [TestFixture]
    public class SoloEndpointTests
    {
        [Test]
        public void TestWritingCaptureFiles_WithData()
        {
            DatasetCapture.ResetSimulation();

            var endpoint = new SoloEndpoint();

            endpoint.basePath = PerceptionSettings.defaultOutputPath;
            endpoint.soloDatasetName = Guid.NewGuid().ToString();

            var frame = new Frame(0, 0, 0, 0);
            var sensor = new RgbSensor(new RgbSensorDefinition("camera", "camera", "camera"), Vector3.zero, Quaternion.identity);

            var texture = new Texture2D(3, 3, TextureFormat.RGB24, false);
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    texture.SetPixel(x, y, Color.blue);
                }
            }
            texture.Apply();

            sensor.buffer = texture.EncodeToPNG();
            frame.sensors.Add(sensor);
            endpoint.FrameGenerated(frame);

            var cp = endpoint.currentPath;

            // verify that image file exists
            var p = PathUtils.CombineUniversal(cp, "sequence.0", "step0.camera.png");
            FileAssert.Exists(p);

            p = PathUtils.CombineUniversal(cp, "sequence.0", "step0.frame_data.json");

            FileAssert.Exists(p);
            var jsonActual = File.ReadAllText(p);
            Assert.IsTrue(jsonActual.Contains("\"filename\": \"step0.camera.png\""));

            Directory.Delete(cp, true);
        }

        [Test]
        public void TestWritingCaptureFiles_WithIndexOffset()
        {
            const int indexOffset = 7;

            DatasetCapture.ResetSimulation();

            var endpoint = new SoloEndpoint();

            endpoint.basePath = PerceptionSettings.defaultOutputPath;
            endpoint.soloDatasetName = Guid.NewGuid().ToString();

            var savedOffset = endpoint.indexOffset;
            endpoint.indexOffset = indexOffset;

            try
            {
                var frame = new Frame(0, 0, 0, 0);
                var sensor = new RgbSensor(new RgbSensorDefinition("camera", "camera", "camera"), Vector3.zero, Quaternion.identity);

                var texture = new Texture2D(3, 3, TextureFormat.RGB24, false);
                texture.Apply();

                sensor.buffer = texture.EncodeToPNG();
                frame.sensors.Add(sensor);
                endpoint.FrameGenerated(frame);

                var cp = endpoint.currentPath;

                // the first sequence of the run is written to sequence.7 rather than sequence.0
                DirectoryAssert.DoesNotExist(PathUtils.CombineUniversal(cp, "sequence.0"));
                FileAssert.Exists(PathUtils.CombineUniversal(cp, $"sequence.{indexOffset}", "step0.camera.png"));

                var p = PathUtils.CombineUniversal(cp, $"sequence.{indexOffset}", "step0.frame_data.json");
                FileAssert.Exists(p);

                // the sequence reported in the frame data has to match the folder it is written to
                var jsonActual = File.ReadAllText(p);
                Assert.IsTrue(jsonActual.Contains($"\"sequence\": {indexOffset}"));

                Directory.Delete(cp, true);
            }
            finally
            {
                endpoint.indexOffset = savedOffset;
            }
        }

        [Test]
        public void TestWritingCaptureFiles_NoData()
        {
            DatasetCapture.ResetSimulation();

            var endpoint = new SoloEndpoint();
            endpoint.basePath = PerceptionSettings.defaultOutputPath;
            endpoint.soloDatasetName = Guid.NewGuid().ToString();

            var frame = new Frame(0, 0, 0, 0);
            var sensor = new RgbSensor(new RgbSensorDefinition("camera", "camera", "camera"), Vector3.zero, Quaternion.identity);

            sensor.buffer = Array.Empty<byte>();
            frame.sensors.Add(sensor);
            endpoint.FrameGenerated(frame);

            var cp = endpoint.currentPath;

            // verify that image file exists
            var p = PathUtils.CombineUniversal(cp, "sequence.0", "step0.camera.png");
            FileAssert.DoesNotExist(p);

            p = PathUtils.CombineUniversal(cp, "sequence.0", "step0.frame_data.json");

            FileAssert.Exists(p);
            var jsonActual = File.ReadAllText(p);
            Assert.IsTrue(jsonActual.Contains("\"filename\": null"));

            Directory.Delete(cp, true);
        }
    }
}
