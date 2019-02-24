
using UnityEngine;

//using Windows.Kinect;

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Text;

#if !UNITY_WSA
using ICSharpCode.SharpZipLib.Zip;
#endif
//using OpenCvSharp;
using UnityEngine.SceneManagement;


/// <summary>
/// KinectInterop is a class containing utility and interop functions, that call the proper sensor interface.
/// </summary>
public class KinectInterop
{
	// order of depth sensor interfaces
	//	public static Type[] SensorInterfaceOrder = new Type[] {
	//		typeof(Kinect2Interface), typeof(Kinect1Interface), typeof(OpenNI2Interface)
	//	};
	public static DepthSensorInterface CurrentSensorInterface = new Kinect2Interface ();

	// graphics shader level
	private static int graphicsShaderLevel = 0;

	
	/// <summary>
	/// Constants used by this class and other K2-components
	/// </summary>
	public static class Constants
	{
		public const int MaxBodyCount = 6;
		public const int MaxJointCount = 25;

		public const float MinTimeBetweenSameGestures = 0.0f;
		public const float PoseCompleteDuration = 1.0f;
		public const float ClickMaxDistance = 0.05f;
		public const float ClickStayDuration = 2.0f;
	}

	// Types of depth sensor platforms
	public enum DepthSensorPlatform : int
	{
		None = 0,
		KinectSDKv2 = 2
	}
	
	// Data structures for interfacing C# with the native wrappers

	[Flags]
	public enum FrameSource : uint
	{
		TypeNone = 0x0,
		TypeBodyIndex = 0x10,
		TypeBody = 0x20,
	}

	public enum JointType : int
	{
		SpineBase = 0,
		SpineMid = 1,
		Neck = 2,
		Head = 3,
		ShoulderLeft = 4,
		ElbowLeft = 5,
		WristLeft = 6,
		HandLeft = 7,
		ShoulderRight = 8,
		ElbowRight = 9,
		WristRight = 10,
		HandRight = 11,
		HipLeft = 12,
		KneeLeft = 13,
		AnkleLeft = 14,
		FootLeft = 15,
		HipRight = 16,
		KneeRight = 17,
		AnkleRight = 18,
		FootRight = 19,
		SpineShoulder = 20,
		HandTipLeft = 21,
		ThumbLeft = 22,
		HandTipRight = 23,
		ThumbRight = 24
		//Count = 25
	}

	public static readonly Vector3[] JointBaseDir = {
		Vector3.zero,
		Vector3.up,
		Vector3.up,
		Vector3.up,
		Vector3.left,
		Vector3.left,
		Vector3.left,
		Vector3.left,
		Vector3.right,
		Vector3.right,
		Vector3.right,
		Vector3.right,
		Vector3.down,
		Vector3.down,
		Vector3.down,
		Vector3.forward,
		Vector3.down,
		Vector3.down,
		Vector3.down,
		Vector3.forward,
		Vector3.up,
		Vector3.left,
		Vector3.forward,
		Vector3.right,
		Vector3.forward
	};

	public enum TrackingState
	{
		NotTracked = 0,
		Inferred = 1,
		Tracked = 2
	}

	public enum HandState
	{
		Unknown = 0,
		NotTracked = 1,
		Open = 2,
		Closed = 3,
		Lasso = 4
	}

	public enum TrackingConfidence
	{
		Low = 0,
		High = 1
	}

	//    [Flags]
	//    public enum ClippedEdges
	//    {
	//        None = 0,
	//        Right = 1,
	//        Left = 2,
	//        Top = 4,
	//        Bottom = 8
	//    }



	/// <summary>
	/// Container for the sensor data, including color, depth, ir and body frames.
	/// </summary>
	public class SensorData
	{
		public DepthSensorInterface sensorInterface;
		public DepthSensorPlatform sensorIntPlatform;

		public int bodyCount;
		public int jointCount;

		public long lastBodyIndexFrameTime = 0;

		public byte selectedBodyIndex = 255;

		public Quaternion sensorRotDetected = Quaternion.identity;


		public float[] bodyIndexBufferData = null;
		public bool bodyIndexBufferReady = false;
		public object bodyIndexBufferLock = new object ();

		public int firstUserIndex = -1;

		public int erodeIterations;
		public int dilateIterations;

		public bool bodyFrameReady = false;
		public object bodyFrameLock = new object ();
		public bool newBodyFrame = false;
		
		public bool isPlayModeEnabled;
		public string playModeData;
		public string playModeHandData;
	}

	/// <summary>
	/// Parameters used for smoothing of the body-joint positions between frames.
	/// </summary>
	public struct SmoothParameters
	{
		public float smoothing;
		public float correction;
		public float prediction;
		public float jitterRadius;
		public float maxDeviationRadius;
	}

	/// <summary>
	/// Container for the body-joint data.
	/// </summary>
	public struct JointData
	{
		// parameters filled in by the sensor interface
		//public JointType jointType;
		public TrackingState trackingState;
		public Vector3 kinectPos;
		public Vector3 position;
		public Quaternion orientation;
		// deprecated

		public Vector3 posPrev;
		public Vector3 posRel;
		public Vector3 posVel;

		// KM calculated parameters
		public Vector3 direction;
		public Quaternion normalRotation;
		public Quaternion mirroredRotation;
		
		// Constraint parameters
		public float lastAngle;
	}

	/// <summary>
	/// Container for the body data.
	/// </summary>
	public struct BodyData
	{
		// parameters filled in by the sensor interface
		public Int64 liTrackingID;
		public Vector3 position;
		public Quaternion orientation;
		// deprecated

		public JointData[] joint;

		// KM calculated parameters
		public Quaternion normalRotation;
		public Quaternion mirroredRotation;
		
		public Vector3 hipsDirection;
		public Vector3 shouldersDirection;
		public float bodyTurnAngle;
		//public float bodyFullAngle;
		public bool isTurnedAround;
		public float turnAroundFactor;

		public Quaternion leftHandOrientation;
		public Quaternion rightHandOrientation;

		public Quaternion headOrientation;

		public HandState leftHandState;
		public TrackingConfidence leftHandConfidence;
		public HandState rightHandState;
		public TrackingConfidence rightHandConfidence;
		
		public uint dwClippedEdges;
		public short bIsTracked;
		public short bIsRestricted;
	}

	/// <summary>
	/// Container for the body frame data.
	/// </summary>
	public struct BodyFrameData
	{
		public Int64 liRelativeTime, liPreviousTime;
		[MarshalAsAttribute (UnmanagedType.ByValArray, SizeConst = 6, ArraySubType = UnmanagedType.Struct)]
		public BodyData[] bodyData;
		//public UnityEngine.Vector4 floorClipPlane;
		public bool bTurnAnalisys;

		public BodyFrameData (int bodyCount, int jointCount)
		{
			liRelativeTime = liPreviousTime = 0;
			//floorClipPlane = UnityEngine.Vector4.zero;
			bTurnAnalisys = false;

			bodyData = new BodyData[bodyCount];

			for (int i = 0; i < bodyCount; i++) {
				bodyData [i].joint = new JointData[jointCount];

				//bodyData [i].leftHandOrientation = Quaternion.identity;
				//bodyData [i].rightHandOrientation = Quaternion.identity;
				//bodyData [i].headOrientation = Quaternion.identity;
			}
		}
	}
	

	// initializes the available sensor interfaces
	public static DepthSensorInterface InitSensorInterface (bool bOnceRestarted, ref bool bNeedRestart)
	{
		return CurrentSensorInterface;
	}

	// opens the default sensor and needed readers
	public static SensorData OpenDefaultSensor (DepthSensorInterface sensorInt, FrameSource dwFlags, float sensorAngle, bool bUseMultiSource)
	{
		SensorData sensorData = null;
		if (sensorInt == null)
			return sensorData;

		try {
			if (sensorData == null) {
				sensorData = sensorInt.OpenDefaultSensor (dwFlags, sensorAngle, bUseMultiSource);

				if (sensorData != null) {
					sensorData.sensorInterface = sensorInt;
					sensorData.sensorIntPlatform = sensorInt.GetSensorPlatform ();
					Debug.Log ("Interface used: " + sensorInt.GetType ().Name);

					Debug.Log ("Shader level: " + SystemInfo.graphicsShaderLevel);
												

				}
			} else {
				sensorInt.FreeSensorInterface (false);
			}
		} catch (Exception ex) {
			Debug.LogError ("Initialization of the sensor failed.");
			Debug.LogError (ex.ToString ());

			try {
				sensorInt.FreeSensorInterface (false);
			} catch (Exception) {
				// do nothing
			}
		}
		

		return sensorData;
	}

	// closes opened readers and closes the sensor
	public static void CloseSensor (SensorData sensorData)
	{

		if (sensorData != null && sensorData.sensorInterface != null) {
			sensorData.sensorInterface.CloseSensor (sensorData);
		}

			
	}

	// invoked periodically to update sensor data, if needed
	public static bool UpdateSensorData (SensorData sensorData)
	{
		bool bResult = false;

		if (sensorData.sensorInterface != null) {
			bResult = sensorData.sensorInterface.UpdateSensorData (sensorData);
		}

		return bResult;
	}
	
	// returns the mirror joint of the given joint
	public static JointType GetMirrorJoint (JointType joint)
	{
		switch (joint) {
		case JointType.ShoulderLeft:
			return JointType.ShoulderRight;
		case JointType.ElbowLeft:
			return JointType.ElbowRight;
		case JointType.WristLeft:
			return JointType.WristRight;
		case JointType.HandLeft:
			return JointType.HandRight;
					
		case JointType.ShoulderRight:
			return JointType.ShoulderLeft;
		case JointType.ElbowRight:
			return JointType.ElbowLeft;
		case JointType.WristRight:
			return JointType.WristLeft;
		case JointType.HandRight:
			return JointType.HandLeft;
					
		case JointType.HipLeft:
			return JointType.HipRight;
		case JointType.KneeLeft:
			return JointType.KneeRight;
		case JointType.AnkleLeft:
			return JointType.AnkleRight;
		case JointType.FootLeft:
			return JointType.FootRight;
					
		case JointType.HipRight:
			return JointType.HipLeft;
		case JointType.KneeRight:
			return JointType.KneeLeft;
		case JointType.AnkleRight:
			return JointType.AnkleLeft;
		case JointType.FootRight:
			return JointType.FootLeft;
					
		case JointType.HandTipLeft:
			return JointType.HandTipRight;
		case JointType.ThumbLeft:
			return JointType.ThumbRight;
			
		case JointType.HandTipRight:
			return JointType.HandTipLeft;
		case JointType.ThumbRight:
			return JointType.ThumbLeft;
		}
	
		return joint;
	}

	// gets new multi source frame
	public static bool GetMultiSourceFrame (SensorData sensorData)
	{
		bool bResult = false;

		if (sensorData.sensorInterface != null) {
			bResult = sensorData.sensorInterface.GetMultiSourceFrame (sensorData);
		}

		return bResult;
	}

	// frees last multi source frame
	public static void FreeMultiSourceFrame (SensorData sensorData)
	{
		if (sensorData.sensorInterface != null) {
			sensorData.sensorInterface.FreeMultiSourceFrame (sensorData);
		}
	}


	// Polls for new skeleton data
	public static bool PollBodyFrame (SensorData sensorData, ref BodyFrameData bodyFrame, ref Matrix4x4 kinectToWorld, bool bIgnoreJointZ)
	{
		bool bNewFrame = false;

		if (sensorData.sensorInterface != null) {

			bNewFrame = sensorData.sensorInterface.PollBodyFrame (sensorData, ref bodyFrame, ref kinectToWorld, bIgnoreJointZ);

			if (bNewFrame) {
				if (bodyFrame.bTurnAnalisys && bodyFrame.liPreviousTime > 0) {
					CalcBodyFrameJointVels (sensorData, ref bodyFrame);
				}

				CalcBodyFrameBoneDirs (sensorData, ref bodyFrame);
				
				// frame is ready
				lock (sensorData.bodyFrameLock) {
					sensorData.bodyFrameReady = true;
				}
			}
		}
		
		return bNewFrame;
	}

	// calculates joint velocities in a body frame
	private static void CalcBodyFrameJointVels (SensorData sensorData, ref BodyFrameData bodyFrame)
	{
		// calculate the inter-frame time
		float frameTime = (float)(bodyFrame.liRelativeTime - bodyFrame.liPreviousTime) / 100000000000f;

		for (int i = 0; i < sensorData.bodyCount; i++) {
			if (bodyFrame.bodyData [i].bIsTracked != 0) {
				for (int j = 0; j < sensorData.jointCount; j++) {
					KinectInterop.JointData jointData = bodyFrame.bodyData [i].joint [j];

					int p = (int)sensorData.sensorInterface.GetParentJoint ((KinectInterop.JointType)j);
					Vector3 parentPos = bodyFrame.bodyData [i].joint [p].position;

					jointData.posRel = jointData.position - parentPos;
					jointData.posVel = frameTime > 0f ? (jointData.position - jointData.posPrev) / frameTime : Vector3.zero;
					jointData.posPrev = jointData.position;

					bodyFrame.bodyData [i].joint [j] = jointData;
				}
			}
		}

	}

	// Calculates all valid bone directions in a body frame
	private static void CalcBodyFrameBoneDirs (SensorData sensorData, ref BodyFrameData bodyFrame)
	{
		for (int i = 0; i < sensorData.bodyCount; i++) {
			if (bodyFrame.bodyData [i].bIsTracked != 0) {
				for (int j = 0; j < sensorData.jointCount; j++) {
					if (j == 0) {
						bodyFrame.bodyData [i].joint [j].direction = Vector3.zero;
					} else {
						int jParent = (int)sensorData.sensorInterface.GetParentJoint ((KinectInterop.JointType)j);
						
						if (bodyFrame.bodyData [i].joint [j].trackingState != TrackingState.NotTracked &&
						   bodyFrame.bodyData [i].joint [jParent].trackingState != TrackingState.NotTracked) {
							bodyFrame.bodyData [i].joint [j].direction = 
								bodyFrame.bodyData [i].joint [j].position - bodyFrame.bodyData [i].joint [jParent].position;
						}
					}
				}
			}
		}

	}
	
	// Recalculates bone directions for the given body
	public static void RecalcBoneDirs (SensorData sensorData, ref BodyData bodyData)
	{
		for (int j = 0; j < bodyData.joint.Length; j++) {
			if (j == 0) {
				bodyData.joint [j].direction = Vector3.zero;
			} else {
				int jParent = (int)sensorData.sensorInterface.GetParentJoint ((KinectInterop.JointType)j);
				
				if (bodyData.joint [j].trackingState != TrackingState.NotTracked &&
				   bodyData.joint [jParent].trackingState != TrackingState.NotTracked) {
					bodyData.joint [j].direction = bodyData.joint [j].position - bodyData.joint [jParent].position;
				}
			}
		}
	}
		

	// copy source file to the target
	public static bool CopyFile (string sourceFilePath, string targetFilePath, ref bool bOneCopied, ref bool bAllCopied)
	{
#if !UNITY_WSA
		FileInfo sourceFile = new FileInfo (sourceFilePath);
		if (!sourceFile.Exists) {
			return false;
		}

		FileInfo targetFile = new FileInfo (targetFilePath);
		if (!targetFile.Directory.Exists) {
			targetFile.Directory.Create ();
		}
		
		if (!targetFile.Exists || targetFile.Length != sourceFile.Length) {
			Debug.Log ("Copying " + sourceFile.Name + "...");
			File.Copy (sourceFilePath, targetFilePath);
			
			bool bFileCopied = File.Exists (targetFilePath);
			
			bOneCopied = bOneCopied || bFileCopied;
			bAllCopied = bAllCopied && bFileCopied;
			
			return bFileCopied;
		}
#endif

		return false;
	}
	
	// Copy a resource file to the target
	public static bool CopyResourceFile (string targetFilePath, string resFileName, ref bool bOneCopied, ref bool bAllCopied)
	{
#if !UNITY_WSA
		TextAsset textRes = Resources.Load (resFileName, typeof(TextAsset)) as TextAsset;
		if (textRes == null) {
			bOneCopied = false;
			bAllCopied = false;
			
			return false;
		}
		
		FileInfo targetFile = new FileInfo (targetFilePath);
		if (!targetFile.Directory.Exists) {
			targetFile.Directory.Create ();
		}
		
		if (!targetFile.Exists || targetFile.Length != textRes.bytes.Length) {
			Debug.Log ("Copying " + resFileName + "...");

			if (textRes != null) {
				using (FileStream fileStream = new FileStream (targetFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
					fileStream.Write (textRes.bytes, 0, textRes.bytes.Length);
				}
				
				bool bFileCopied = File.Exists (targetFilePath);
				
				bOneCopied = bOneCopied || bFileCopied;
				bAllCopied = bAllCopied && bFileCopied;
				
				return bFileCopied;
			}
		}
#endif

		return false;
	}

	// Unzips resource file to the target path
	public static bool UnzipResourceDirectory (string targetDirPath, string resZipFileName, string checkForDir)
	{
#if !UNITY_WSA
		if (checkForDir != string.Empty && Directory.Exists (checkForDir)) {
			return false;
		}

		TextAsset textRes = Resources.Load (resZipFileName, typeof(TextAsset)) as TextAsset;
		if (textRes == null || textRes.bytes.Length == 0) {
			return false;
		}

		Debug.Log ("Unzipping " + resZipFileName + "...");

		// get the resource steam
		MemoryStream memStream = new MemoryStream (textRes.bytes);

		// fix invalid code page 437 error
		ZipConstants.DefaultCodePage = 0;

		using (ZipInputStream s = new ZipInputStream (memStream)) {
			ZipEntry theEntry;
			while ((theEntry = s.GetNextEntry ()) != null) {
				//Debug.Log(theEntry.Name);
				
				string directoryName = targetDirPath + Path.GetDirectoryName (theEntry.Name);
				string fileName = Path.GetFileName (theEntry.Name);

				if (!Directory.Exists (directoryName)) {
					// create directory
					Directory.CreateDirectory (directoryName);
				}

				if (fileName != string.Empty && !fileName.EndsWith (".meta")) {
					string targetFilePath = directoryName + "/" + fileName;

					using (FileStream streamWriter = File.Create (targetFilePath)) {
						int size = 2048;
						byte[] data = new byte[2048];
						
						while (true) {
							size = s.Read (data, 0, data.Length);
							
							if (size > 0) {
								streamWriter.Write (data, 0, size);
							} else {
								break;
							}
						}
					}
				}
			}
		}

		// close the resource stream
		//memStream.Close();
		memStream.Dispose ();

		return true;
#else
		return false;
#endif	
	}

	// Unzips resource file to the target path
	public static bool UnzipResourceFiles (Dictionary<string, string> dictFilesToUnzip, string resZipFileName, 
	                                      ref bool bOneCopied, ref bool bAllCopied)
	{
#if !UNITY_WSA		
		TextAsset textRes = Resources.Load (resZipFileName, typeof(TextAsset)) as TextAsset;
		if (textRes == null || textRes.bytes.Length == 0) {
			bOneCopied = false;
			bAllCopied = false;

			return false;
		}
		
		//Debug.Log("Unzipping " + resZipFileName + "...");
		
		// get the resource steam
		MemoryStream memStream = new MemoryStream (textRes.bytes);
		
		// fix invalid code page 437 error
		ZipConstants.DefaultCodePage = 0;
		
		using (ZipInputStream s = new ZipInputStream (memStream)) {
			ZipEntry theEntry;
			while ((theEntry = s.GetNextEntry ()) != null) {
				//Debug.Log(theEntry.Name);

				if (dictFilesToUnzip.ContainsKey (theEntry.Name)) {
					string targetFilePath = dictFilesToUnzip [theEntry.Name];

					string directoryName = Path.GetDirectoryName (targetFilePath);
					string fileName = Path.GetFileName (theEntry.Name);
					
					if (!Directory.Exists (directoryName)) {
						// create directory
						Directory.CreateDirectory (directoryName);
					}

					FileInfo targetFile = new FileInfo (targetFilePath);
					bool bTargetFileNewOrUpdated = !targetFile.Exists || targetFile.Length != theEntry.Size;
					
					if (fileName != string.Empty && bTargetFileNewOrUpdated) {
						using (FileStream streamWriter = File.Create (targetFilePath)) {
							int size = 2048;
							byte[] data = new byte[2048];
							
							while (true) {
								size = s.Read (data, 0, data.Length);
								
								if (size > 0) {
									streamWriter.Write (data, 0, size);
								} else {
									break;
								}
							}
						}
						
						bool bFileCopied = File.Exists (targetFilePath);
						
						bOneCopied = bOneCopied || bFileCopied;
						bAllCopied = bAllCopied && bFileCopied;
					}
				}

			}
		}
		
		// close the resource stream
		//memStream.Close();
		memStream.Dispose ();
		
		return true;
#else
		return false;
#endif
	}
	
	// returns the unzipped file size in bytes, or -1 if the entry is not found in the zip
	public static long GetUnzippedEntrySize (string resZipFileName, string sEntryName)
	{
#if !UNITY_WSA
		TextAsset textRes = Resources.Load (resZipFileName, typeof(TextAsset)) as TextAsset;
		if (textRes == null || textRes.bytes.Length == 0) {
			return -1;
		}
		
		// get the resource steam
		MemoryStream memStream = new MemoryStream (textRes.bytes);
		
		// fix invalid code page 437 error
		ZipConstants.DefaultCodePage = 0;
		long entryFileSize = -1;
		
		using (ZipInputStream s = new ZipInputStream (memStream)) {
			ZipEntry theEntry;
			while ((theEntry = s.GetNextEntry ()) != null) {
				if (theEntry.Name == sEntryName) {
					entryFileSize = theEntry.Size;
					break;
				}
				
			}
		}
		

		memStream.Dispose ();
		
		return entryFileSize;
#else
		return -1;
#endif
	}
	
	// returns true if the project is running on 64-bit architecture, false if 32-bit
	public static bool Is64bitArchitecture ()
	{
		int sizeOfPtr = Marshal.SizeOf (typeof(IntPtr));
		return (sizeOfPtr > 4);
	}

	// returns the target dll path for the current platform (x86 or x64)
	public static string GetTargetDllPath (string sAppPath, bool bIs64bitApp)
	{
		string sTargetPath = sAppPath;

		return sTargetPath;
	}

	// cleans up objects and restarts the current level
	public static void RestartLevel (GameObject parentObject, string callerName)
	{
		Debug.Log (callerName + " is restarting level...");

		// destroy parent object if any
		if (parentObject) {
			GameObject.Destroy (parentObject);
		}

		// clean up memory assets
		Resources.UnloadUnusedAssets ();
		GC.Collect ();

		//if(Application.HasProLicense() && Application.isEditor)
		{
#if UNITY_EDITOR
			// refresh the assets database
			UnityEditor.AssetDatabase.Refresh ();
#endif
		}
		
		// reload the same level
		SceneManager.LoadScene (SceneManager.GetActiveScene ().buildIndex);
		//SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}

	// sets the graphics shader level
	public static void SetGraphicsShaderLevel (int shaderLevel)
	{
		graphicsShaderLevel = shaderLevel;
	}

	// checks if DirectX11/Direct3D-11 is turned on or not
	public static bool IsDirectX11Available ()
	{
		return (graphicsShaderLevel >= 50);
	}

	// copies open-cv dlls to the root folder, if needed
	public static bool IsOpenCvAvailable (ref bool bNeedRestart)
	{
		bNeedRestart = false;

		if (IsDirectX11Available ()) {
			// use shaders
			return true;
		}
		return true;
	}



	// DLL Imports for native library functions
	[DllImport ("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
	static extern IntPtr LoadLibrary ([MarshalAs (UnmanagedType.LPStr)]string lpFileName);

	[DllImport ("kernel32", SetLastError = true)]
	static extern bool FreeLibrary (IntPtr hModule);

	// load the native dll to ensure the library is loaded
	public static bool LoadNativeLib (string sLibName)
	{
		string sTargetPath = KinectInterop.GetTargetDllPath (".", Is64bitArchitecture ());
		string sFullLibPath = sTargetPath + "/" + sLibName;

		IntPtr hLibrary = LoadLibrary (sFullLibPath);

		return (hLibrary != IntPtr.Zero);
	}
	
	// unloads and deletes native library
	public static void DeleteNativeLib (string sLibName, bool bUnloadLib)
	{
		string sTargetPath = KinectInterop.GetTargetDllPath (".", Is64bitArchitecture ());
		string sFullLibPath = sTargetPath + "/" + sLibName;
		
		if (bUnloadLib) {
			IntPtr hLibrary = LoadLibrary (sFullLibPath);
			
			if (hLibrary != IntPtr.Zero) {
				FreeLibrary (hLibrary);
				FreeLibrary (hLibrary);
			}
		}
		
		try {
			// delete file
			if (File.Exists (sFullLibPath)) {
				File.Delete (sFullLibPath);
			}
		} catch (Exception) {
			Debug.Log ("Could not delete file: " + sFullLibPath);
		}
	}

	// universal windows platform specific functions

	#if UNITY_WSA
	[DllImport("kernelbase")]
	public static extern void Sleep(int dwMilliseconds);

#else
	[DllImport ("kernel32")]
	public static extern void Sleep (int dwMilliseconds);
	#endif


	public static bool IsFileExists (string sFilePath, long iFileSize)
	{
#if UNITY_WSA
		return File.Exists(sFilePath);
#else
		System.IO.FileInfo targetFile = new System.IO.FileInfo (sFilePath);
		return targetFile.Exists && targetFile.Length == iFileSize;
#endif
	}


	public static string GetEnvironmentVariable (string sEnvVar)
	{
#if !UNITY_WSA
		return System.Environment.GetEnvironmentVariable (sEnvVar);
#else
		return String.Empty;
#endif
	}

}