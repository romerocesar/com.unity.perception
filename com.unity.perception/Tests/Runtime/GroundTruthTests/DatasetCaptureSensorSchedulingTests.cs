using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.TestTools;

namespace GroundTruthTests
{
    [TestFixture]
    public class DatasetCaptureSensorSchedulingTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1;
            DatasetCapture.ResetSimulation();
        }

        [UnityTest]
        public IEnumerator FramesScheduledBySensorConfig()
        {
            var ego = DatasetCapture.RegisterEgo("ego");
            var firstCaptureTime = 1.5f;
            var period = .4f;
            DatasetCapture.RegisterSensor(ego, "cam", "", period, firstCaptureTime);

            float[] deltaTimeSamplesExpected =
            {
                firstCaptureTime,
                period,
                period,
                period
            };
            float[] deltaTimeSamples = new float[deltaTimeSamplesExpected.Length];
            for (int i = 0; i < deltaTimeSamples.Length; i++)
            {
                yield return null;
                Assert.AreEqual(deltaTimeSamplesExpected[i], Time.deltaTime, 0.0001f);
            }
        }

        [UnityTest]
        public IEnumerator FramesScheduled_WithTimeScale_ResultsInProperDeltaTime()
        {
            var ego = DatasetCapture.RegisterEgo("ego");
            var firstCaptureTime = 2f;
            var period = 1f;

            var timeScale = 2;
            Time.timeScale = timeScale;
            DatasetCapture.RegisterSensor(ego, "cam", "", period, firstCaptureTime);

            float[] deltaTimeSamplesExpected =
            {
                timeScale * firstCaptureTime,
                timeScale * period,
                timeScale * period,
                timeScale * period
            };
            float[] deltaTimeSamples = new float[deltaTimeSamplesExpected.Length];
            for (int i = 0; i < deltaTimeSamples.Length; i++)
            {
                yield return null;
                Assert.AreEqual(deltaTimeSamplesExpected[i], Time.deltaTime, 0.0001f);
            }
        }

        [UnityTest]
        public IEnumerator ChangingTimeScale_CausesDebugError()
        {
            var ego = DatasetCapture.RegisterEgo("ego");
            DatasetCapture.RegisterSensor(ego, "cam", "", 1f, 2f);

            yield return null;
            Time.timeScale = 5;
            yield return null;
            LogAssert.Expect(LogType.Error, new Regex("Time\\.timeScale may not change mid-sequence.*"));
        }

        [UnityTest]
        public IEnumerator ChangingTimeScale_DuringStartNewSequence_Succeeds()
        {
            var ego = DatasetCapture.RegisterEgo("ego");
            DatasetCapture.RegisterSensor(ego, "cam", "", 1f, 2f);

            yield return null;
            Time.timeScale = 1;
            DatasetCapture.StartNewSequence();
            yield return null;
        }

        [Ignore("Changing timeScale mid-sequence is not supported")]
        [UnityTest]
        public IEnumerator FramesScheduled_WithChangingTimeScale_ResultsInProperDeltaTime()
        {
            var ego = DatasetCapture.RegisterEgo("ego");
            var firstCaptureTime = 2f;
            var period = 1f;
            float[] newTimeScalesPerFrame =
            {
                2f,
                10f,
                .01f,
                1f
            };
            DatasetCapture.RegisterSensor(ego, "cam", "", period, firstCaptureTime);

            float[] deltaTimeSamplesExpected =
            {
                newTimeScalesPerFrame[0] * firstCaptureTime,
                newTimeScalesPerFrame[1] * period,
                newTimeScalesPerFrame[2] * period,
                newTimeScalesPerFrame[3] * period
            };
            float[] deltaTimeSamples = new float[deltaTimeSamplesExpected.Length];
            for (int i = 0; i < deltaTimeSamples.Length; i++)
            {
                Time.timeScale = newTimeScalesPerFrame[i];
                yield return null;
                Assert.AreEqual(deltaTimeSamplesExpected[i], Time.deltaTime, 0.0001f);
            }
        }

        [UnityTest]
        public IEnumerator ResetSimulation_ResetsCaptureDeltaTime()
        {
            var ego = DatasetCapture.RegisterEgo("ego");
            DatasetCapture.RegisterSensor(ego, "cam", "", 4, 10);
            yield return null;
            Assert.AreEqual(10, Time.captureDeltaTime);
            DatasetCapture.ResetSimulation();
            Assert.AreEqual(0, Time.captureDeltaTime);
        }

        [UnityTest]
        public IEnumerator ShouldCaptureThisFrame_ReturnsTrueOnProperFrames()
        {
            var ego = DatasetCapture.RegisterEgo("ego");
            var firstCaptureTime1 = 10;
            var frequencyInMs1 = 4;
            var sensor1 = DatasetCapture.RegisterSensor(ego, "cam", "1", frequencyInMs1, firstCaptureTime1);

            var firstCaptureTime2 = 10;
            var frequencyInMs2 = 6;
            var sensor2 = DatasetCapture.RegisterSensor(ego, "cam", "2", frequencyInMs2, firstCaptureTime2);

            var sensor3 = DatasetCapture.RegisterSensor(ego, "cam", "3", 1, 1);
            sensor3.Enabled = false;

            (float deltaTime, bool sensor1ShouldCapture, bool sensor2ShouldCapture)[] samplesExpected =
            {
                ((float)firstCaptureTime1, true, true),
                (4, true, false),
                (2, false, true),
                (2, true, false),
                (4, true, true)
            };
            var samplesActual = new(float deltaTime, bool sensor1ShouldCapture, bool sensor2ShouldCapture)[samplesExpected.Length];
            for (int i = 0; i < samplesActual.Length; i++)
            {
                yield return null;
                samplesActual[i] = (Time.deltaTime, sensor1.ShouldCaptureThisFrame, sensor2.ShouldCaptureThisFrame);
            }

            CollectionAssert.AreEqual(samplesExpected, samplesActual);
        }

        [Test]
        public void Enabled_StartsTrue()
        {
            var sensor1 = DatasetCapture.RegisterSensor(DatasetCapture.RegisterEgo(""), "cam", "1", 1, 1);
            Assert.IsTrue(sensor1.Enabled);
        }
    }
}
