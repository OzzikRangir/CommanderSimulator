using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public interface DepthSensorInterface
{
	// returns the depth sensor platform
	KinectInterop.DepthSensorPlatform GetSensorPlatform();

	// initializes libraries and resources needed by this sensor interface
	// returns true if the resources are successfully initialized, false otherwise
	bool InitSensorInterface(bool bCopyLibs, ref bool bNeedRestart);

	// releases the resources and libraries used by this interface
	void FreeSensorInterface(bool bDeleteLibs);

	// checks if there is available sensor on this interface
	// returns true if there are available sensors on this interface, false otherwise
	bool IsSensorAvailable();
	
	// returns the number of available sensors, controlled by this interface
	int GetSensorsCount();

	// opens the default sensor and inits needed resources. returns new sensor-data object
	KinectInterop.SensorData OpenDefaultSensor(KinectInterop.FrameSource dwFlags, float sensorAngle, bool bUseMultiSource);

	// closes the sensor and frees used resources
	void CloseSensor(KinectInterop.SensorData sensorData);

	// this method is invoked periodically to update sensor data, if needed
	// returns true if update is successful, false otherwise
	bool UpdateSensorData(KinectInterop.SensorData sensorData);

	// gets next multi source frame, if one is available
	// returns true if there is a new multi-source frame, false otherwise
	bool GetMultiSourceFrame(KinectInterop.SensorData sensorData);

	// frees the resources taken by the last multi-source frame
	void FreeMultiSourceFrame(KinectInterop.SensorData sensorData);

	// polls for new body/skeleton frame. must fill in all needed body and joints' elements (tracking state and position)
	// returns true if new body frame is available, false otherwise
	bool PollBodyFrame(KinectInterop.SensorData sensorData, ref KinectInterop.BodyFrameData bodyFrame, ref Matrix4x4 kinectToWorld, bool bIgnoreJointZ);

	// performs sensor-specific fixes of joint positions and orientations
	void FixJointOrientations(KinectInterop.SensorData sensorData, ref KinectInterop.BodyData bodyData);

	// returns the index of the given joint in joint's array
	int GetJointIndex(KinectInterop.JointType joint);
	
	// returns the parent joint of the given joint
	KinectInterop.JointType GetParentJoint(KinectInterop.JointType joint);
	
	// returns the next joint in the hierarchy, as to the given joint
	KinectInterop.JointType GetNextJoint(KinectInterop.JointType joint);

	// gets the head position of the specified user. returns true on success, false otherwise
	bool GetHeadPosition(long userId, ref Vector3 headPos);

	// gets the head rotation of the specified user. returns true on success, false otherwise
	bool GetHeadRotation(long userId, ref Quaternion headRot);

	// returns true if BR-manager supports high resolution background removal
	bool IsBRHiResSupported();


	
}
