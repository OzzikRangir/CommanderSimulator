#if !(UNITY_WSA_10_0 && NETFX_CORE)
using UnityEngine;
using System.Collections;
using Windows.Kinect;
using System.Runtime.InteropServices;
using Microsoft.Kinect.Face;
using System.Collections.Generic;
using System;

public class Kinect2Interface : DepthSensorInterface
{
	// change this to false, if you aren't using Kinect-v2 only and want KM to check for available sensors
	public static bool sensorAlwaysAvailable = true;

	private KinectInterop.FrameSource sensorFlags;
	public KinectSensor kinectSensor;
	public CoordinateMapper coordMapper;
	
	private BodyFrameReader bodyFrameReader;
	private BodyIndexFrameReader bodyIndexFrameReader;
	private ColorFrameReader colorFrameReader;
	private DepthFrameReader depthFrameReader;
	private InfraredFrameReader infraredFrameReader;
	
	private MultiSourceFrameReader multiSourceFrameReader;
	private MultiSourceFrame multiSourceFrame;

	private BodyFrame msBodyFrame = null;
	private BodyIndexFrame msBodyIndexFrame = null;
	private ColorFrame msColorFrame = null;
	private DepthFrame msDepthFrame = null;
	private InfraredFrame msInfraredFrame = null;

	private int bodyCount;
	private Body[] bodyData;

	public KinectInterop.DepthSensorPlatform GetSensorPlatform ()
	{
		return KinectInterop.DepthSensorPlatform.KinectSDKv2;
	}

	public bool InitSensorInterface (bool bCopyLibs, ref bool bNeedRestart)
	{
		bool bOneCopied = false, bAllCopied = true;
		string sTargetPath = KinectInterop.GetTargetDllPath (".", KinectInterop.Is64bitArchitecture ()) + "/";

		if (!bCopyLibs) {
			// check if the native library is there
			string sTargetLib = sTargetPath + "KinectUnityAddin.dll";
			bNeedRestart = false;

			string sZipFileName = !KinectInterop.Is64bitArchitecture () ? "KinectV2UnityAddin.x86.zip" : "KinectV2UnityAddin.x64.zip";
			long iTargetSize = KinectInterop.GetUnzippedEntrySize (sZipFileName, "KinectUnityAddin.dll");

			return KinectInterop.IsFileExists (sTargetLib, iTargetSize);
		}
		

		Debug.Log ("x64-architecture detected.");

		//KinectInterop.CopyResourceFile(sTargetPath + "KinectUnityAddin.dll", "KinectUnityAddin.dll.x64", ref bOneCopied, ref bAllCopied);
			
		Dictionary<string, string> dictFilesToUnzip = new Dictionary<string, string> ();
		dictFilesToUnzip ["KinectUnityAddin.dll"] = sTargetPath + "KinectUnityAddin.dll";
		dictFilesToUnzip ["Kinect20.Face.dll"] = sTargetPath + "Kinect20.Face.dll";
		dictFilesToUnzip ["KinectFaceUnityAddin.dll"] = sTargetPath + "KinectFaceUnityAddin.dll";

		dictFilesToUnzip ["Kinect20.VisualGestureBuilder.dll"] = sTargetPath + "Kinect20.VisualGestureBuilder.dll";
		dictFilesToUnzip ["KinectVisualGestureBuilderUnityAddin.dll"] = sTargetPath + "KinectVisualGestureBuilderUnityAddin.dll";
		dictFilesToUnzip ["vgbtechs/AdaBoostTech.dll"] = sTargetPath + "vgbtechs/AdaBoostTech.dll";
		dictFilesToUnzip ["vgbtechs/RFRProgressTech.dll"] = sTargetPath + "vgbtechs/RFRProgressTech.dll";
		dictFilesToUnzip ["msvcp110.dll"] = sTargetPath + "msvcp110.dll";
		dictFilesToUnzip ["msvcr110.dll"] = sTargetPath + "msvcr110.dll";

		KinectInterop.UnzipResourceFiles (dictFilesToUnzip, "KinectV2UnityAddin.x64.zip", ref bOneCopied, ref bAllCopied);


		KinectInterop.UnzipResourceDirectory (sTargetPath, "NuiDatabase.zip", sTargetPath + "NuiDatabase");

		bNeedRestart = (bOneCopied && bAllCopied);

		return true;
	}

	public void FreeSensorInterface (bool bDeleteLibs)
	{
		if (bDeleteLibs) {
			KinectInterop.DeleteNativeLib ("KinectUnityAddin.dll", true);
			KinectInterop.DeleteNativeLib ("msvcp110.dll", false);
			KinectInterop.DeleteNativeLib ("msvcr110.dll", false);
		}
	}

	public bool IsSensorAvailable ()
	{
		KinectSensor sensor = KinectSensor.GetDefault ();

		if (sensor != null) {
			if (sensorAlwaysAvailable) {
				sensor = null;
				return true;
			}

			if (!sensor.IsOpen) {
				sensor.Open ();
			}

			float fWaitTime = Time.realtimeSinceStartup + 3f;
			while (!sensor.IsAvailable && Time.realtimeSinceStartup < fWaitTime) {
				// wait for availability
			}
			
			bool bAvailable = sensor.IsAvailable;

			if (sensor.IsOpen) {
				sensor.Close ();
			}
			
			fWaitTime = Time.realtimeSinceStartup + 3f;
			while (sensor.IsOpen && Time.realtimeSinceStartup < fWaitTime) {
				// wait for sensor to close
			}

			sensor = null;
			return bAvailable;
		}

		return false;
	}

	public int GetSensorsCount ()
	{
		int numSensors = 0;

		KinectSensor sensor = KinectSensor.GetDefault ();
		if (sensor != null) {
			if (!sensor.IsOpen) {
				sensor.Open ();
			}
			
			float fWaitTime = Time.realtimeSinceStartup + 3f;
			while (!sensor.IsAvailable && Time.realtimeSinceStartup < fWaitTime) {
				// wait for availability
			}
			
			numSensors = sensor.IsAvailable ? 1 : 0;

			if (sensor.IsOpen) {
				sensor.Close ();
			}
			
			fWaitTime = Time.realtimeSinceStartup + 3f;
			while (sensor.IsOpen && Time.realtimeSinceStartup < fWaitTime) {
				// wait for sensor to close
			}
		}

		return numSensors;
	}

	public KinectInterop.SensorData OpenDefaultSensor (KinectInterop.FrameSource dwFlags, float sensorAngle, bool bUseMultiSource)
	{
		KinectInterop.SensorData sensorData = new KinectInterop.SensorData ();
		sensorFlags = dwFlags;
		
		kinectSensor = KinectSensor.GetDefault ();
		if (kinectSensor == null)
			return null;
		
		coordMapper = kinectSensor.CoordinateMapper;

		this.bodyCount = kinectSensor.BodyFrameSource.BodyCount;
		sensorData.bodyCount = this.bodyCount;
		sensorData.jointCount = 25;

		
		if ((dwFlags & KinectInterop.FrameSource.TypeBody) != 0) {
			if (!bUseMultiSource)
				bodyFrameReader = kinectSensor.BodyFrameSource.OpenReader ();
			
			bodyData = new Body[sensorData.bodyCount];
		}

		
		//if(!kinectSensor.IsOpen)
		{
			//Debug.Log("Opening sensor, available: " + kinectSensor.IsAvailable);
			kinectSensor.Open ();
		}

		float fWaitTime = Time.realtimeSinceStartup + 3f;
		while (!kinectSensor.IsAvailable && Time.realtimeSinceStartup < fWaitTime) {
			// wait for sensor to open
		}

		Debug.Log ("K2-sensor " + (kinectSensor.IsOpen ? "opened" : "closed") +
		", available: " + kinectSensor.IsAvailable);

		if (bUseMultiSource && dwFlags != KinectInterop.FrameSource.TypeNone && kinectSensor.IsOpen) {
			multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader ((FrameSourceTypes)((int)dwFlags & 0x3F));
		}
		
		return sensorData;
	}

	public void CloseSensor (KinectInterop.SensorData sensorData)
	{
		if (coordMapper != null) {
			coordMapper = null;
		}
		
		if (bodyFrameReader != null) {
			bodyFrameReader.Dispose ();
			bodyFrameReader = null;
		}
		
		if (bodyIndexFrameReader != null) {
			bodyIndexFrameReader.Dispose ();
			bodyIndexFrameReader = null;
		}
		
		if (colorFrameReader != null) {
			colorFrameReader.Dispose ();
			colorFrameReader = null;
		}
		
		if (depthFrameReader != null) {
			depthFrameReader.Dispose ();
			depthFrameReader = null;
		}
		
		if (infraredFrameReader != null) {
			infraredFrameReader.Dispose ();
			infraredFrameReader = null;
		}
		
		if (multiSourceFrameReader != null) {
			multiSourceFrameReader.Dispose ();
			multiSourceFrameReader = null;
		}
		
		if (kinectSensor != null) {
			//if (kinectSensor.IsOpen)
			{
				//Debug.Log("Closing sensor, available: " + kinectSensor.IsAvailable);
				kinectSensor.Close ();
			}
			
			float fWaitTime = Time.realtimeSinceStartup + 3f;
			while (kinectSensor.IsOpen && Time.realtimeSinceStartup < fWaitTime) {
				// wait for sensor to close
			}
			
			Debug.Log ("K2-sensor " + (kinectSensor.IsOpen ? "opened" : "closed") +
			", available: " + kinectSensor.IsAvailable);
			
			kinectSensor = null;
		}
	}

	public bool UpdateSensorData (KinectInterop.SensorData sensorData)
	{
		return true;
	}

	public bool GetMultiSourceFrame (KinectInterop.SensorData sensorData)
	{
		if (multiSourceFrameReader != null) {
			multiSourceFrame = multiSourceFrameReader.AcquireLatestFrame ();

			if (multiSourceFrame != null) {
				// try to get all frames at once
				msBodyFrame = (sensorFlags & KinectInterop.FrameSource.TypeBody) != 0 ? multiSourceFrame.BodyFrameReference.AcquireFrame () : null;
				msBodyIndexFrame = (sensorFlags & KinectInterop.FrameSource.TypeBodyIndex) != 0 ? multiSourceFrame.BodyIndexFrameReference.AcquireFrame () : null;

				bool bAllSet =
					((sensorFlags & KinectInterop.FrameSource.TypeBody) == 0 || msBodyFrame != null) &&
					((sensorFlags & KinectInterop.FrameSource.TypeBodyIndex) == 0 || msBodyIndexFrame != null);


				if (!bAllSet) {
					// release all frames
					if (msBodyFrame != null) {
						msBodyFrame.Dispose ();
						msBodyFrame = null;
					}

					if (msBodyIndexFrame != null) {
						msBodyIndexFrame.Dispose ();
						msBodyIndexFrame = null;
					}

					if (msColorFrame != null) {
						msColorFrame.Dispose ();
						msColorFrame = null;
					}

					if (msDepthFrame != null) {
						msDepthFrame.Dispose ();
						msDepthFrame = null;
					}

					if (msInfraredFrame != null) {
						msInfraredFrame.Dispose ();
						msInfraredFrame = null;
					}
				}
//				else
//				{
//					bool bNeedBody = (sensorFlags & KinectInterop.FrameSource.TypeBody) != 0;
//					bool bNeedBodyIndex = (sensorFlags & KinectInterop.FrameSource.TypeBodyIndex) != 0;
//					bool bNeedColor = (sensorFlags & KinectInterop.FrameSource.TypeColor) != 0;
//					bool bNeedDepth = (sensorFlags & KinectInterop.FrameSource.TypeDepth) != 0;
//					bool bNeedInfrared = (sensorFlags & KinectInterop.FrameSource.TypeInfrared) != 0;
//
//					bAllSet = true;
//				}
			}

			return (multiSourceFrame != null);
		}
		
		return false;
	}

	public void FreeMultiSourceFrame (KinectInterop.SensorData sensorData)
	{
		// release all frames
		if (msBodyFrame != null) {
			msBodyFrame.Dispose ();
			msBodyFrame = null;
		}
		
		if (msBodyIndexFrame != null) {
			msBodyIndexFrame.Dispose ();
			msBodyIndexFrame = null;
		}
		
		if (msColorFrame != null) {
			msColorFrame.Dispose ();
			msColorFrame = null;
		}
		
		if (msDepthFrame != null) {
			msDepthFrame.Dispose ();
			msDepthFrame = null;
		}
		
		if (msInfraredFrame != null) {
			msInfraredFrame.Dispose ();
			msInfraredFrame = null;
		}

		if (multiSourceFrame != null) {
			multiSourceFrame = null;
		}
	}

	public bool PollBodyFrame (KinectInterop.SensorData sensorData, ref KinectInterop.BodyFrameData bodyFrame, 
	                           ref Matrix4x4 kinectToWorld, bool bIgnoreJointZ)
	{
		bool bNewFrame = false;
		
		if ((multiSourceFrameReader != null && multiSourceFrame != null) ||
		   bodyFrameReader != null) {
			BodyFrame frame = multiSourceFrame != null ? msBodyFrame : 
				bodyFrameReader.AcquireLatestFrame ();

			if (frame != null) {
				frame.GetAndRefreshBodyData (bodyData);

				bodyFrame.liPreviousTime = bodyFrame.liRelativeTime;
				bodyFrame.liRelativeTime = frame.RelativeTime.Ticks;


				frame.Dispose ();
				frame = null;
				
				for (int i = 0; i < sensorData.bodyCount; i++) {
					Body body = bodyData [i];
					
					if (body == null) {
						bodyFrame.bodyData [i].bIsTracked = 0;
						continue;
					}
					
					bodyFrame.bodyData [i].bIsTracked = (short)(body.IsTracked ? 1 : 0);
					
					if (body.IsTracked) {
						// transfer body and joints data
						bodyFrame.bodyData [i].liTrackingID = (long)body.TrackingId;

						// cache the body joints (following the advice of Brian Chasalow)
						Dictionary<Windows.Kinect.JointType, Windows.Kinect.Joint> bodyJoints = body.Joints;

						for (int j = 0; j < sensorData.jointCount; j++) {
							Windows.Kinect.Joint joint = bodyJoints [(Windows.Kinect.JointType)j];
							KinectInterop.JointData jointData = bodyFrame.bodyData [i].joint [j];
							
							//jointData.jointType = (KinectInterop.JointType)j;
							jointData.trackingState = (KinectInterop.TrackingState)joint.TrackingState;

							if ((int)joint.TrackingState != (int)TrackingState.NotTracked) {
								float jPosZ = (bIgnoreJointZ && j > 0) ? bodyFrame.bodyData [i].joint [0].kinectPos.z : joint.Position.Z;
								jointData.kinectPos = new Vector3 (joint.Position.X, joint.Position.Y, joint.Position.Z);
								jointData.position = kinectToWorld.MultiplyPoint3x4 (new Vector3 (joint.Position.X, joint.Position.Y, jPosZ));
							}
							
							jointData.orientation = Quaternion.identity;
//							Windows.Kinect.Vector4 vQ = body.JointOrientations[jointData.jointType].Orientation;
//							jointData.orientation = new Quaternion(vQ.X, vQ.Y, vQ.Z, vQ.W);
							
							if (j == 0) {
								bodyFrame.bodyData [i].position = jointData.position;
								bodyFrame.bodyData [i].orientation = jointData.orientation;
							}

							bodyFrame.bodyData [i].joint [j] = jointData;
						}

						// tranfer hand states
						bodyFrame.bodyData [i].leftHandState = (KinectInterop.HandState)body.HandLeftState;
						bodyFrame.bodyData [i].leftHandConfidence = (KinectInterop.TrackingConfidence)body.HandLeftConfidence;
						
						bodyFrame.bodyData [i].rightHandState = (KinectInterop.HandState)body.HandRightState;
						bodyFrame.bodyData [i].rightHandConfidence = (KinectInterop.TrackingConfidence)body.HandRightConfidence;
					}
				}
				
				bNewFrame = true;
			}
		}
		
		return bNewFrame;
	}

	public void FixJointOrientations (KinectInterop.SensorData sensorData, ref KinectInterop.BodyData bodyData)
	{
		// no fixes are needed
	}
		
		
	// returns the index of the given joint in joint's array or -1 if joint is not applicable
	public int GetJointIndex (KinectInterop.JointType joint)
	{
		return (int)joint;
	}
	
	//	// returns the joint at given index
	//	public KinectInterop.JointType GetJointAtIndex(int index)
	//	{
	//		return (KinectInterop.JointType)(index);
	//	}
	
	// returns the parent joint of the given joint
	public KinectInterop.JointType GetParentJoint (KinectInterop.JointType joint)
	{
		switch (joint) {
		case KinectInterop.JointType.SpineBase:
			return KinectInterop.JointType.SpineBase;
				
		case KinectInterop.JointType.Neck:
			return KinectInterop.JointType.SpineShoulder;
				
		case KinectInterop.JointType.SpineShoulder:
			return KinectInterop.JointType.SpineMid;
				
		case KinectInterop.JointType.ShoulderLeft:
		case KinectInterop.JointType.ShoulderRight:
			return KinectInterop.JointType.SpineShoulder;
				
		case KinectInterop.JointType.HipLeft:
		case KinectInterop.JointType.HipRight:
			return KinectInterop.JointType.SpineBase;
				
		case KinectInterop.JointType.HandTipLeft:
			return KinectInterop.JointType.HandLeft;
				
		case KinectInterop.JointType.ThumbLeft:
			return KinectInterop.JointType.WristLeft;
			
		case KinectInterop.JointType.HandTipRight:
			return KinectInterop.JointType.HandRight;

		case KinectInterop.JointType.ThumbRight:
			return KinectInterop.JointType.WristRight;
		}
			
		return (KinectInterop.JointType)((int)joint - 1);
	}
	
	// returns the next joint in the hierarchy, as to the given joint
	public KinectInterop.JointType GetNextJoint (KinectInterop.JointType joint)
	{
		switch (joint) {
		case KinectInterop.JointType.SpineBase:
			return KinectInterop.JointType.SpineMid;
		case KinectInterop.JointType.SpineMid:
			return KinectInterop.JointType.SpineShoulder;
		case KinectInterop.JointType.SpineShoulder:
			return KinectInterop.JointType.Neck;
		case KinectInterop.JointType.Neck:
			return KinectInterop.JointType.Head;
				
		case KinectInterop.JointType.ShoulderLeft:
			return KinectInterop.JointType.ElbowLeft;
		case KinectInterop.JointType.ElbowLeft:
			return KinectInterop.JointType.WristLeft;
		case KinectInterop.JointType.WristLeft:
			return KinectInterop.JointType.HandLeft;
		case KinectInterop.JointType.HandLeft:
			return KinectInterop.JointType.HandTipLeft;
				
		case KinectInterop.JointType.ShoulderRight:
			return KinectInterop.JointType.ElbowRight;
		case KinectInterop.JointType.ElbowRight:
			return KinectInterop.JointType.WristRight;
		case KinectInterop.JointType.WristRight:
			return KinectInterop.JointType.HandRight;
		case KinectInterop.JointType.HandRight:
			return KinectInterop.JointType.HandTipRight;
				
		case KinectInterop.JointType.HipLeft:
			return KinectInterop.JointType.KneeLeft;
		case KinectInterop.JointType.KneeLeft:
			return KinectInterop.JointType.AnkleLeft;
		case KinectInterop.JointType.AnkleLeft:
			return KinectInterop.JointType.FootLeft;
				
		case KinectInterop.JointType.HipRight:
			return KinectInterop.JointType.KneeRight;
		case KinectInterop.JointType.KneeRight:
			return KinectInterop.JointType.AnkleRight;
		case KinectInterop.JointType.AnkleRight:
			return KinectInterop.JointType.FootRight;
		}
		
		return joint;  // in case of end joint - Head, HandTipLeft, HandTipRight, FootLeft, FootRight
	}

	public bool GetHeadPosition (long userId, ref Vector3 headPos)
	{
		for (int i = 0; i < this.bodyCount; i++) {
			if (bodyData [i].TrackingId == (ulong)userId && bodyData [i].IsTracked) {
				CameraSpacePoint vHeadPos = bodyData [i].Joints [Windows.Kinect.JointType.Head].Position;

				if (vHeadPos.Z > 0f) {
					headPos.x = vHeadPos.X;
					headPos.y = vHeadPos.Y;
					headPos.z = vHeadPos.Z;
					
					return true;
				}
			}
		}
		
		return false;
	}

	public bool GetHeadRotation(long userId, ref Quaternion headRot)
	{
		for (int i = 0; i < this.bodyCount; i++) {
			if (bodyData [i].TrackingId == (ulong)userId && bodyData [i].IsTracked) {
				Windows.Kinect.Vector4 vHeadRot =  bodyData [i].JointOrientations [Windows.Kinect.JointType.Head].Orientation;
				

				if (vHeadRot.W > 0f) {
					headRot = new Quaternion (vHeadRot.X, vHeadRot.Y, vHeadRot.Z, vHeadRot.W);
					return true;
				}

			}
		}
		return false;
	}

	public bool IsBRHiResSupported ()
	{
		return true;
	}
	
}
#endif