﻿/*
Blotch3D (formerly GWin3D) Copyright (c) 1999-2018 Kelly Loum, all rights reserved except those granted in the following license.

Microsoft Public License (MS-PL)
This license governs use of the accompanying software. If you use the software, you
accept this license. If you do not accept the license, do not use the software.

1. Definitions
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the
same meaning here as under U.S. copyright law.
A "contribution" is the original software, or any additions or changes to the software.
A "contributor" is any person that distributes its contribution under this license.
"Licensed patents" are a contributor's patent claims that read directly on its contribution.

2. Grant of Rights
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.

3. Conditions and Limitations
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.
*/



/*
TODO:

Particle systems
Reflective surfaces (including ripples for water)
Write a fairly elaborate program to tweak and test everything



*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Blotch
{
	/// <summary>
	/// This holds everything having to do with an output device. BlWindow3D creates one of these for itself.
	/// This derives from MonoGame GraphicsDeviceManager.
	/// </summary>
	public class BlGraphicsDeviceManager : GraphicsDeviceManager, ICloneable
	{
		/// <summary>
		/// This is the view matrix. Normally you would use the higher-level functions
		/// #Eye, #LookAt, #CameraUp, #SetCameraToSprite, and #DoDefaultGui intead of changing this directly.
		/// </summary>
		public Microsoft.Xna.Framework.Matrix View;

		/// <summary>
		/// The Projection matrix. Normally you would use the higher-level functions
		/// #Zoom, #Aspect, #NearClip, or #FarClip intead of changing this directly.
		/// </summary>
		public Microsoft.Xna.Framework.Matrix Projection;

		/// <summary>
		/// The vector between #Eye and #LookAt. Writes to #Eye and #LookAt and calls to
		/// #SetCameraToSprite cause this to be updated. Also
		/// see #CameraForwardNormalized and #CameraForwardMag.
		/// </summary>
		public Vector3 CameraForward { get; private set; }

		/// <summary>
		/// Normalized form of #CameraForward.  Writes to #Eye and #LookAt, and calls to
		/// #SetCameraToSprite cause this to be updated. Also see #CameraForward and #CameraForwardMag.
		/// </summary>
		public Vector3 CameraForwardNormalized { get; private set; }

		/// <summary>
		/// The magnitude of #CameraForward.  Writes to #Eye and #LookAt, and calls to
		/// #SetCameraToSprite cause this to be updated. Also see #CameraForward and #CameraForwardNormalized.
		/// </summary>
		public float CameraForwardMag { get; private set; }

		/// <summary>
		/// Camera Up vector. Initially set to +Z. #ResetCamera and #SetCameraToSprite updates this.
		/// </summary>
		public Vector3 CameraUp;

		/// <summary>
		/// Camera Right vector.  Writes to #Eye and #LookAt, and calls to
		/// #SetCameraToSprite cause this to be updated.
		/// </summary>
		public Vector3 CameraRight { get; private set; }

		/// <summary>
		/// Causes #DoDefaultGui to prevent the Z component of #CameraForwardNormalized from falling below this value.
		/// For example, set this to zero so that #DoDefaultGui won't allow the camera to look downward
		/// </summary>
		public double DefGuiMinLookZ = -1;

		/// <summary>
		/// Caues #DoDefaultGui to prevent the Z component of #CameraForwardNormalized from rising above this value.
		/// For example, set this to zero so that #DoDefaultGui won't allow the camera to look upward
		/// </summary>
		public double DefGuiMaxLookZ = 1;

		/// <summary>
		/// Assign DepthStencilState to this to enable depth buffering
		/// </summary>
		public DepthStencilState DepthStencilStateEnabled = new DepthStencilState()
		{
			DepthBufferEnable = true,
			DepthBufferWriteEnable = true,
			DepthBufferFunction = CompareFunction.LessEqual
		};

		/// <summary>
		/// Assign DepthStencilState to this to disable depth buffering
		/// </summary>
		public DepthStencilState DepthStencilStateDisabled = new DepthStencilState()
		{
			DepthBufferEnable = false,
			DepthBufferWriteEnable = false,
			DepthBufferFunction = CompareFunction.Always
		};

		/// <summary>
		/// The current camera position. See #TargetEye.
		/// </summary>
		public Vector3 Eye
		{
			get
			{
				return _eye;
			}
			private set
			{
				_eye = value;
				UpdateLookVec();
			}
		}
		Vector3 _eye;

		/// <summary>
		/// The current camera LookAt position. See #TargetLookAt.
		/// </summary>
		public Vector3 LookAt
		{
			get
			{
				return _lookAt;
			}
			private set
			{
				_lookAt = value;
				UpdateLookVec();
			}
		}
		Vector3 _lookAt;

		/// <summary>
		/// The point that #Eye migrates to, according to #CameraSpeed. This is normally controlled by #DoDefaultGui, but can also be controlled
		/// by the AdjustCameraxxx methods. The easiest way to control the camera
		/// exactly (including camera roll), is to use SetCameraToSprite and then set the sprite's matrix as desired.
		/// </summary>
		public Vector3 TargetEye;

		/// <summary>
		/// The point that #LookAt migrates to, according to #CameraSpeed. This is normally controlled by #DoDefaultGui, but can also be controlled
		/// by the AdjustCameraxxx methods. The easiest way to control the camera
		/// exactly (including camera roll), is to use SetCameraToSprite and then set the sprite's matrix as desired.
		/// </summary>
		public Vector3 TargetLookAt;

		/// <summary>
		/// The responsiveness of the camera position to changes in #TargetEye and #TargetLookAt. A value of 0 means it doesn't respond to
		/// changes, 1 means it immediately responds. See #Eye and #LookAt for more information.
		/// </summary>
		public double CameraSpeed = .4;

		void UpdateLookVec()
		{
			CameraForward = _lookAt - _eye;
			CameraForwardMag = CameraForward.Length();
			var lookAtNorm = CameraForward;
			lookAtNorm.Normalize();
			CameraForwardNormalized = lookAtNorm;
		}

		/// <summary>
		/// The field of view, in degrees.
		/// </summary>
		public double Zoom=45;
		/// <summary>
		/// The aspect ratio.
		/// </summary>
		public double Aspect=2;

		/// <summary>
		/// Current aspect ratio. Same as #Aspect unless #Aspect==0.
		/// </summary>
		public double CurrentAspect { get; private set; }

		/// <summary>
		/// The near clipping plane. If 0 then autoclip. If negative then auto-clip down to a limit of -NearClip.
		/// (Auto-clipping has a one-frame latency. A mechanism is employed in the camera control methods to somewhat alleviate
		/// this, but in certain cases you may still see a one frame dropout in visibility.) See the description for the
		/// depth buffer for more information.
		/// </summary>
		public double NearClip = 0;
		/// <summary>
		/// The far clipping plane. If 0 then autoclip. If negative then auto-clip up to a limit of -FarClip.
		/// (Auto-clipping has a one-frame latency. A mechanism is employed in the camera control methods to somewhat alleviate
		/// this, but in certain cases you may still see a one frame dropout in visibility.) See the description for the
		/// depth buffer for more information.
		/// </summary>
		public double FarClip = 0;
		/// <summary>
		/// Current value of near clipping plane. See #NearClip.
		/// </summary>
		public double CurrentNearClip {get; private set;}
		/// <summary>
		/// Current value of far clipping plane. See #FarClip.
		/// </summary>
		public double CurrentFarClip { get; private set; }

		/// <summary>
		/// Increase far clipping and decrease near clipping by this much. Use this to alleviate certain auto-clipping
		/// artifacts. See NearClip and FarClip.
		/// </summary>
		public double ClipRangeExcess = 5;

		/// <summary>
		/// The background color.
		/// </summary>
		public Microsoft.Xna.Framework.Color ClearColor=new Microsoft.Xna.Framework.Color(0,0,.1f);

		/// <summary>
		/// Distance to the nearest sprite, less its radius. Note this is set to a very large number by
		/// #PrepareDraw, and then as BlWindow3D#FrameDraw is called it is set more reasonably.
		/// </summary>
		public double MinCamDistance { get; private set; }
		/// <summary>
		/// Distance to the farthest sprite, plus its radius. Note this is set to a very small number by
		/// #PrepareDraw, and then as BlWindow3D#FrameDraw is called it is set more reasonably.
		/// </summary>
		public double MaxCamDistance { get; private set; }

		MouseState PrevMouseState = new MouseState();
		Vector2 PrevMousePos = new Vector2();
		bool LastActive = false;

		/// <summary>
		/// How fast #DoDefaultGui should auto-rotate the scene.
		/// </summary>
		public double AutoRotate = 0;

		/// <summary>
		/// How much time between consecutive frames.
		/// </summary>
		public double FramePeriod = 1/60.0;

		DateTime LastUpdateTime = DateTime.Now;
		double SleepTime = 0;

		/// <summary>
		/// The directional lights. Note: The BasicEffect shader only supports the first three.
		/// To handle more lights, you'll need to write your own shader.
		/// </summary>
		public List<Light> Lights = new List<Light>();

		/// <summary>
		/// The ambient light color. If null, no ambient light is enabled. Note: There is no ambient color
		/// for a BlSprite. Both diffuse and ambient light illuminates the model's Color. See the BlSprite#Color member.
		/// </summary>
		public Vector3? AmbientLightColor = new Vector3(.1f, .1f, .1f);

		/// <summary>
		/// If not null, color of fog.
		/// </summary>
		public Vector3? FogColor = null;

		/// <summary>
		/// How far away fog starts. See #FogColor.
		/// </summary>
		public float fogStart = 1;

		/// <summary>
		/// How far away fog ends. See #FogColor.
		/// </summary>
		public float fogEnd = 10;

		/// <summary>
		/// The BlWindow3D associated with this object.
		/// </summary>
		public BlWindow3D Window;

		/// <summary>
		/// A SpriteBatch for use by certain text and texture drawing methods.
		/// </summary>
		public SpriteBatch SpriteBatch=null;

		/// <summary>
		/// </summary>
		/// <param name="window">The BlWindow3D object for which this is to be the #BlGraphicsDeviceManager</param>
		public BlGraphicsDeviceManager(BlWindow3D window):base(window)
		{
			CreationThread = Thread.CurrentThread.ManagedThreadId;
			this.Window = window;
			window.Window.AllowUserResizing = true;
			window.IsMouseVisible = true;

			// use 60Hz frame rate (unless a sleep to slow it down further is in the Draw method)
			window.IsFixedTimeStep = false;

			var light = new Light();
			light.LightDiffuseColor = new Vector3(.9f, .4f, .4f);
			light.LightDirection = new Vector3(1, 0, -1);
			Lights.Add(light);

			light = new Light();
			light.LightDiffuseColor = new Vector3(.4f, .4f, .9f);
			light.LightDirection = new Vector3(-1, 0,-1);
			Lights.Add(light);

			ResetCamera();
		}
		/// <summary>
		/// For internal use only. Apps should not normally call this.
		/// This initializes some values AFTER the BlWindow3D has been created.
		/// </summary>
		public void Initialize()
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.Initialize() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			GraphicsDevice.BlendState = BlendState.AlphaBlend;
			//GraphicsDevice.DepthStencilState = DepthStencilState.Default;
			GraphicsProfile = GraphicsProfile.HiDef;
			PreferMultiSampling = true;

			//RasterizerState rasterizerState = new RasterizerState();
			//rasterizerState.CullMode = CullMode.None;
			//GraphicsDevice.RasterizerState = rasterizerState;
			GraphicsProfile = GraphicsProfile.HiDef;
			GraphicsDevice.PresentationParameters.MultiSampleCount = 8;
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;
			ApplyChanges();
		}

		/// <summary>
		/// Informs the auto-clipping code of an object that should be visible within the clipping limits. This is
		/// mainly for internal use. Application code should control clipping with NearClip and FarClip.
		/// </summary>
		/// <param name="s">The sprite that should be included in the auto-clipping code</param>
		public void ExtendClippingTo(BlSprite s)
		{
			var near = s.CamDistance - s.BoundSphere.Value.Radius;
			var far = s.CamDistance + s.BoundSphere.Value.Radius;

			var distDif = s.CamDistance - s.PrevCamDistance;

			if (distDif > 0)
			{
				far += 1.2*distDif;
			}
			else
			{
				near += 1.2*distDif;
			}

			if (MaxCamDistance < far)
			{
				MaxCamDistance = far;
			}

			if (MinCamDistance > near)
			{
				MinCamDistance = near;
			}

			if (MinCamDistance < 1e-38)
				MinCamDistance = 1e-38;
			
			//Console.WriteLine("{0} {1} {2}", s.Name, s.CamDistance, s.BoundSphere.Value.Radius);
		}
		/// <summary>
		/// Sets a sprite's BlSprite#Matrix to the current camera position and orientation. You could use this to
		/// implement a HUD, for example. Note: This only works correctly if the sprite has no parent (and is thus drawn
		/// directly) or it's parents are untransformed. If all you want is to set the sprite's position (but NOT orientation)
		/// to the camera, then set the sprite's Matrix.Translation = graphics.#Eye
		/// </summary>
		/// <param name="sprite">The sprite that should be connected to the camera</param>
		public void SetSpriteToCamera(BlSprite sprite)
		{
			sprite.Matrix.Translation = Eye;
			sprite.Matrix.Forward = CameraForwardNormalized;
			sprite.Matrix.Up = CameraUp;
			sprite.Matrix.Right = CameraRight;
		}
		/// <summary>
		/// Sets the camera position and orientation to the current position and orientation of a sprite. You could
		/// use for cockpit view, for example. Note that the camera will lag sprite movement unless the following is done:
		/// For every frame you must first calculate the sprite's position and orientation, call this function, and then draw everything.
		/// </summary>
		/// <param name="sprite">The sprite that the camera should be connected to</param>
		public void SetCameraToSprite(BlSprite sprite)
		{
			TargetEye = sprite.Matrix.Translation;
			Eye = TargetEye;
			TargetLookAt = sprite.Matrix.Forward;
			LookAt = TargetLookAt;
			CameraUp = sprite.Matrix.Up;
		}
		/// <summary>
		/// Sets the #Zoom.
		/// If dif is zero, then there is no change in zoom.
		/// Normally one would set zoom with the Zoom field. This is mainly for internal use.
		/// </summary>
		/// <param name="dif">How much to zoom camera (plus = magnify, minus = reduce)</param>
		public void AdjustCameraZoom(double dif)
		{
			Zoom *= 1 + dif * .001;
			if (Zoom >= 179.9)
				Zoom = 179.9;

		}
		/// <summary>
		/// Migrates the current camera dolly (distance from #LookAt) according to dif.
		/// If dif is zero, then there is no change in dolly.
		/// </summary>
		/// <param name="dif">How much to dolly camera (plus = toward #LookAt, minus = away)</param>
		public void AdjustCameraDolly(double dif)
		{
			var vec = TargetLookAt - TargetEye;
			TargetEye += vec * -(float)dif * .0006f;
		}
		/// <summary>
		/// Adjusts camera truck (movement relative to camera direction) according to difX and difY.
		/// if difX and difY are zero, then truck position isn't changed.
		/// </summary>
		/// <param name="difX">How much to truck the camera horizontally</param>
		/// <param name="difY">How much to truck the camera vertically</param>
		public void AdjustCameraTruck(double difX, double difY=0)
		{
			var truckSpeed = .00004f * CameraForwardMag * Zoom;

			var CameraForwardXY = new Vector3(CameraForward.X, CameraForward.Y, 0);
			if (CameraForwardXY.Length() < 1e-10)
				CameraForwardXY = new Vector3(0, 1, 0);

			var CameraForwardXYOrtho = new Vector3(CameraForwardXY.Y, -CameraForwardXY.X, 0);

			CameraForwardXY.Normalize();
			CameraForwardXYOrtho.Normalize();

			var lookVecXY = CameraForwardXY;
			var lookVecXYOrtho = CameraForwardXYOrtho;

			if (CameraForward.Z > 0)
				difY = -difY;

			lookVecXY *= (float)(difY * truckSpeed);
			lookVecXYOrtho *= (float)(-difX * truckSpeed);

			var truck = lookVecXY + lookVecXYOrtho;

			TargetEye += truck;
			TargetLookAt += truck;

		}

		/// <summary>
		/// Adjusts camera rotation about the #LookAt point according to difX and difY.
		/// if difX and difY are zero, then rotation isn't changed.
		/// </summary>
		/// <param name="difX">How much to rotate the camera horizontally</param>
		/// <param name="difY">How much to rotate the camera vertically</param>
		public void AdjustCameraRotation(double difX, double difY=0)
		{
			// get yaw and pitch
			var yaw = (float)(difX * .004);
			var pitch = (float)(difY * .004);

			for (int n = 0; n < 20; n++)
			{
				// get vector from TargetLookAt to TargetEye
				var cameraForward = TargetEye - TargetLookAt;

				// get its magnitude
				var mag = cameraForward.Length();

				// rotate it per yaw
				var m = Microsoft.Xna.Framework.Matrix.CreateRotationZ(-yaw);
				cameraForward = Vector3.Transform(cameraForward, m);

				// increase pitch rate when user wants to leave zenith
				var cameraForwardNorm = cameraForward;
				cameraForwardNorm.Normalize();
				var zMag = cameraForwardNorm.Z;
				if (zMag * pitch > 0)
				{
					zMag = Math.Abs(zMag);
					var pitchRate = (float)Math.Pow(zMag, 26);
					if (pitchRate > .8)
						pitchRate = .8f;
					pitch *= 1 - pitchRate;
				}

				// adjust eye height per pitch
				cameraForward.Z += pitch * mag;

				// don't let its length change 
				var lengthChangeRatio = mag / cameraForward.Length();
				cameraForward *= lengthChangeRatio;

				cameraForwardNorm = -cameraForward;
				cameraForwardNorm.Normalize();

				if ((cameraForwardNorm.Z > DefGuiMinLookZ) && (cameraForwardNorm.Z < DefGuiMaxLookZ))
				{
					// move eye back to new Eye
					TargetEye = cameraForward + TargetLookAt;
					break;
				}

				pitch = 0;
			}

		}
		/// <summary>
		/// Adjusts camera pan (changing direction of camera) according to difX and difY.
		/// if difX and difY are zero, then pan direction isn't changed.
		/// </summary>
		/// <param name="difX">How much to pan horizontally</param>
		/// <param name="difY">How much to pan vertically</param>
		public void AdjustCameraPan(double difX, double difY=0)
		{
			// get yaw and pitch
			var yaw = (float)(-difX * .001);
			var pitch = (float)(-difY * .001);

			for (int n = 0; n < 20; n++)
			{
				// get vector from TargetLookAt to TargetEye
				var cameraForward = TargetEye - TargetLookAt;

				// get its magnitude
				var mag = cameraForward.Length();

				// rotate it per yaw
				var m = Microsoft.Xna.Framework.Matrix.CreateRotationZ(-yaw);
				cameraForward = Vector3.Transform(cameraForward, m);

				// increase pitch rate when user wants to leave zenith
				var cameraForwardNorm = cameraForward;
				cameraForwardNorm.Normalize();
				var zMag = cameraForwardNorm.Z;
				if (zMag * pitch > 0)
				{
					zMag = Math.Abs(zMag);
					var pitchRate = (float)Math.Pow(zMag, 26);
					if (pitchRate > .8)
						pitchRate = .8f;
					pitch *= 1 - pitchRate;
				}

				// adjust eye height per pitch
				cameraForward.Z += pitch * mag;

				// don't let its length change 
				var lengthChangeRatio = mag / cameraForward.Length();
				cameraForward *= lengthChangeRatio;

				cameraForwardNorm = -cameraForward;
				cameraForwardNorm.Normalize();

				if ((cameraForwardNorm.Z > DefGuiMinLookZ) && (cameraForwardNorm.Z < DefGuiMaxLookZ))
				{
					// move eye back to new Eye
					TargetLookAt = -cameraForward + TargetEye;
					break;
				}

				pitch = 0;
			}

		}

		/// <summary>
		/// Updates #Eye, #LookAt, etc. according to mouse and certain key input. Specifically:
		/// Wheel=Dolly, CTRL-wheel=Zoom, Left-drag=Truck, Right-drag=Rotate, CTRL-left-drag=Pan, Esc=Reset.
		/// Also, SHIFT causes all the previous controls to be fine rather than coarse. If CTRL is pressed
		/// and mouse left or right button is clicked, then returns a ray into window
		/// at mouse position. To control each camera attribute individually and programatically or override the
		/// GUI controls, use AdjustCameraZoom, AdjustCameraDolly, AdjustCameraRotation, AdjustCameraPan,
		/// AdjustCameraTruck, ResetCamera, and/or SetCameraToSprite. Or see the more basic fields of Zoom, Aspect,
		/// TargetEye, and TargetLookAt.
		/// </summary>
		/// <returns>If a mouse left or right click occurred, returns the Ray into the screen at that position. Otherwise
		/// returns null</returns>
		public Ray? DoDefaultGui()
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.DoDefaultGui() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			Ray? ray = null;

			try
			{
				if (AutoRotate != 0)
				{
					// get vector from TargetLookAt to TargetEye
					var eye = TargetEye - TargetLookAt;

					// rotate it by AutoRotate
					var m = Microsoft.Xna.Framework.Matrix.CreateRotationZ((float)AutoRotate);
					eye = Vector3.Transform(eye, m);

					// move eye back to new Eye
					TargetEye = eye + TargetLookAt;
				}

				if (Window.IsActive)
				{
					var keyState = Keyboard.GetState();
					var mouseState = Mouse.GetState();
					var mousePos = new Vector2(mouseState.X, mouseState.Y);
					var mousePosDif = mousePos - PrevMousePos;
					var scrollDif = (PrevMouseState.ScrollWheelValue - mouseState.ScrollWheelValue);
					var mouseDifY = (mouseState.Y - PrevMouseState.Y);
					var mouseDifX = (mouseState.X - PrevMouseState.X);

					if(!LastActive && Window.IsActive)
					{
						scrollDif = 0;
					}

					float rate = 1;
					if (keyState.IsKeyDown(Keys.LeftShift) || keyState.IsKeyDown(Keys.RightShift))
						rate = .01f;

					if (scrollDif != 0)
					{
						// Zoom
						if (keyState.IsKeyDown(Keys.LeftControl) || keyState.IsKeyDown(Keys.RightControl))
							AdjustCameraZoom(scrollDif * rate);
						else
							// Dolly
							AdjustCameraDolly(scrollDif * rate);
					}


					// Rotate
					if (mouseState.RightButton == ButtonState.Pressed && PrevMouseState.RightButton == ButtonState.Pressed)
						AdjustCameraRotation(mouseDifX * rate, mouseDifY * rate);

					// Pan
					if (mouseState.LeftButton == ButtonState.Pressed && PrevMouseState.LeftButton == ButtonState.Pressed)
						if (keyState.IsKeyDown(Keys.LeftControl) || keyState.IsKeyDown(Keys.RightControl))
							AdjustCameraPan(mouseDifX * rate, mouseDifY * rate);
						else
							// Truck
							AdjustCameraTruck(mouseDifX * rate, mouseDifY * rate);


					// Reset
					if (keyState.IsKeyDown(Keys.Escape))
					{
						ResetCamera();
					}

					// Pick
					if (keyState.IsKeyDown(Keys.LeftControl) || keyState.IsKeyDown(Keys.RightControl))
					{
						if (
							(mouseState.LeftButton == ButtonState.Pressed && PrevMouseState.LeftButton != ButtonState.Pressed)
							||
							(mouseState.RightButton == ButtonState.Pressed && PrevMouseState.RightButton != ButtonState.Pressed)
						)
						{
							ray = CalculateRay(mousePos);
						}
					}

					PrevMouseState = mouseState;
					PrevMousePos = mousePos;
				}
			}
			catch
			{
					ResetCamera();
			}

			LastActive = Window.IsActive;
			return ray;
		}
		/// <summary>
		/// Sets #Eye. #LookAt, etc. back to default starting position.
		/// </summary>
		public void ResetCamera()
		{
			Eye = new Vector3(0, -5f, 5);
			LookAt = new Vector3(0, 0, 0);
			CameraUp = Vector3.UnitZ;
			Zoom = 50;
			//Aspect = 2;

			TargetEye = Eye;
			TargetLookAt = LookAt;

			SetCameraRollToZero();
		}

		/// <summary>
		/// Sets the camera 'roll' to be level with the XY plane
		/// </summary>
		public void SetCameraRollToZero()
		{
			// TBD also handle when camera is pointing straight up or straight down -KGL
			// TBD make all the polarities correct -KGL

			//
			// Figure a "Right" vector
			//
			Vector3 right = Vector3.Zero;

			// if CameraForward is not on the XY plane...
			if(CameraForward.Z != 0)
			{
				// Make sure Z is always negative so cross product is always on the right
				var cameraForward = CameraForward;
				if (cameraForward.Z > 0)
					cameraForward.Z *= -1;

				// figure a CameraForward on the XY plane
				var cameraForwardXY = new Vector3(CameraForward.X, CameraForward.Y, 0);

				// Get a vector at right angles those
				right = Vector3.Cross(cameraForward, cameraForwardXY);
			}
			else // if CameraForward IS in the XY plane
			{
				right = new Vector3(CameraForward.Y, -CameraForward.X, 0);
			}

			// save official "Right" vector
			right.Normalize();
			CameraRight = right;

			//
			// Now figure the Up vector
			//
			var up = Vector3.Cross(right, CameraForward);

			up.Normalize();

			CameraUp = up;
		}

		/// <summary>
		/// Defines a light. See the #Lights field. The default BasicShader supports up to three lights.
		/// </summary>
		public class Light
		{
			public Vector3? LightDirection = new Vector3(1, 0, 0);
			public Vector3 LightDiffuseColor = new Vector3(1, 0, 1);
			public Vector3 LightSpecularColor = new Vector3(0, 1, 0);
		}

		/// <summary>
		/// Returns a ray that that goes from the near clipping plane to the far clipping plane, at the specified
		/// window position.
		/// </summary>
		/// <param name="windowPosition">The window's pixel coordinates</param>
		/// <returns>The Ray into the window at the specified pixel coordinates</returns>
		public Ray CalculateRay(Vector2 windowPosition)
		{
			Vector3 nearPoint = GraphicsDevice.Viewport.Unproject(new Vector3(windowPosition.X,windowPosition.Y, 0.0f),
					Projection,
					View,
					Microsoft.Xna.Framework.Matrix.Identity);

			Vector3 farPoint = GraphicsDevice.Viewport.Unproject(new Vector3(windowPosition.X,windowPosition.Y, 1.0f),
					Projection,
					View,
					Microsoft.Xna.Framework.Matrix.Identity);

			Vector3 direction = farPoint - nearPoint;
			direction.Normalize();

			return new Ray(nearPoint, direction);
		}

		/// <summary>
		/// Returns the window coordinates of the specified sprite.
		/// </summary>
		/// <param name="sprite">The sprite to get the window coordinates of</param>
		/// <returns>The window coordinates of the sprite, in pixels</returns>
		public Vector3 GetWindowCoordinates(BlSprite sprite)
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.GetWindowCoordinates() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			return GraphicsDevice.Viewport.Project(Vector3.Zero, Projection, View, sprite.AbsoluteMatrix);
		}

		/// <summary>
		/// Returns a Texture2D containing the specified text.
		/// It's up to the caller to Dispose the returned texture.
		/// </summary>
		/// <param name="text">The text to write to the texture</param>
		/// <param name="font">Font to use</param>
		/// <param name="color">If specified, color of the text. (Default is white)</param>
		/// <param name="backColor">If specified, background color, like Color.Transparent. If null, then do not clear
		/// the background)</param>
		/// <returns>The texture (as a RenderTarget2D). Caller is responsible for Disposing this!</returns>
		public Texture2D TextToTexture(
			string text,
			SpriteFont font,
			Microsoft.Xna.Framework.Color? color = null,
			Microsoft.Xna.Framework.Color? backColor = null
		)
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.TextToTexture() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			// we don't handle empty strings so well
			if (text == "")
				text = " ";

			var v = font.MeasureString(text);
			var renderTarget = new RenderTarget2D(GraphicsDevice, (int)(v.X+.99), (int)(v.Y+.99));

			var renderTargets = GraphicsDevice.GetRenderTargets();
			GraphicsDevice.SetRenderTarget(renderTarget);

			if (backColor != null)
				GraphicsDevice.Clear((Microsoft.Xna.Framework.Color)backColor);

			DrawText(text, font, new Vector2(),color);

			GraphicsDevice.SetRenderTargets(renderTargets);

			return renderTarget;
		}
		/// <summary>
		/// Draws a texture in the window.
		/// </summary>
		/// <param name="texture">The texture to draw</param>
		/// <param name="windowRect">The X and Y window location, in pixels</param>
		/// <param name="color">Foreground color of the font</param>
		public void DrawTexture(Texture2D texture, Rectangle windowRect, Microsoft.Xna.Framework.Color? color = null)
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.DrawTexture() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			if (SpriteBatch == null)
				SpriteBatch = new SpriteBatch(GraphicsDevice);

			if (color == null)
				color = new Microsoft.Xna.Framework.Color(0xFFFFFFFF);

			try
			{
				SpriteBatch.Begin();
				SpriteBatch.Draw(texture, windowRect, (Microsoft.Xna.Framework.Color)color);
			}
			finally
			{
				SpriteBatch.End();
			}
		}
		/// <summary>
		/// Draws text on the window.
		/// </summary>
		/// <param name="text">The text to draw</param>
		/// <param name="font">The font to use (typically created from SpriteFont content with Content.Load<SpriteFont>(...) )</param>
		/// <param name="windowPos">The X and Y window location, in pixels</param>
		/// <param name="color">Foreground color of the font</param>
		public void DrawText(string text, SpriteFont font, Vector2 windowPos, Microsoft.Xna.Framework.Color? color = null)
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.DrawText() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			if (SpriteBatch == null)
				SpriteBatch = new SpriteBatch(GraphicsDevice);

			if (color == null)
				color = new Microsoft.Xna.Framework.Color(0xFFFFFFFF);

			try
			{
				SpriteBatch.Begin();
				SpriteBatch.DrawString(font, text, windowPos, (Microsoft.Xna.Framework.Color)color);
			}
			finally
			{
				SpriteBatch.End();
			}
		}
		/// <summary>
		/// Loads a texture directly from an image file.
		/// </summary>
		/// <param name="fileName">An image file of any standard type supported by MonoGame (jpg, png, etc.)</param>
		/// <param name="mirrorY">If true, then mirror Y</param>
		/// <returns>The texture that was loaded</returns>
		public Texture2D LoadFromImageFile(string fileName,bool mirrorY=false)
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.LoadFromImageFile() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			Texture2D texture = null;
			using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
			{
				texture = Texture2D.FromStream(GraphicsDevice, fileStream);
			}

			if(mirrorY)
			{
				var totalPixels = texture.Width * texture.Height;
				var pixels = new Color[totalPixels];

				texture.GetData(pixels);

				var width = texture.Width;
				var height = texture.Height;

				Parallel.For(0, width, (x) =>
				{
					Parallel.For(0, height/2, (y) =>
					{
						var i = x + width * y;
						var im = x + width * (height - y - 1);
						var p = pixels[i];
						pixels[i] = pixels[im];
						pixels[im] = p;
					});
				});
				texture.SetData(pixels);
			}

			return texture;
		}

		void UpdateViewDirection()
		{
			var vec = Eye - LookAt;

			LookAt = (1-(float)CameraSpeed) * LookAt + (float)CameraSpeed * TargetLookAt;
			Eye = (1 - (float)CameraSpeed) * Eye + (float)CameraSpeed * TargetEye;

			var newVec = Eye - LookAt;

			var difDist = newVec.Length() - vec.Length();

			MaxCamDistance += difDist;
			MinCamDistance += difDist;

			SetCameraRollToZero();
		}

		/// <summary>
		/// This is automatically called once at the beginning of your BlWindow3D#FrameDraw method. It calculates the latest #View and
		/// #Projection settings according to the current camera specifications (#Zoom, #Aspect, #Eye, #LookAt, etc.), and
		/// if firstCallInDraw is true it also may sleep in order to obey FramePeriod. It must also be called explicitly after
		/// any changes to the camera settings made later in the BlWindow3D#FrameDraw method. Only in the first call
		/// should firstCallInDraw be true, and in any subsequent calls it should be false.
		/// </summary>
		/// <param name="firstCallInDraw">True indicates this method should also sleep in order to obey FramePeriod.</param>
		public void PrepareDraw(bool firstCallInDraw=true)
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.PrepareDraw() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			if (firstCallInDraw)
			{
				// regulate frame rate
				var now = DateTime.Now;
				var elapsed = now - LastUpdateTime;

				LastUpdateTime = now;

				var sleepTime = 2 * FramePeriod - elapsed.TotalSeconds;

				SleepTime = .1 * sleepTime + .9 * SleepTime;

				if (SleepTime > 0)
				{
					Thread.Sleep((int)(SleepTime * 1000));
				}
			}

			UpdateViewDirection();

			var width = Window.Window.ClientBounds.Width;
			var height = Window.Window.ClientBounds.Height;
			if (width != PreferredBackBufferWidth || height != PreferredBackBufferHeight)
			{
				PreferredBackBufferWidth = Window.Window.ClientBounds.Width;
				PreferredBackBufferHeight = Window.Window.ClientBounds.Height;
			}


			// auto clipping

			double nearLimit = 1e-2;

			if (NearClip <= 0)
			{
				CurrentNearClip = MinCamDistance;

				if (NearClip < 0)
					nearLimit = -NearClip;
			}
			else
			{
				CurrentNearClip = NearClip;
			}

			double farLimit = 1e29;

			if (FarClip <= 0)
			{
				CurrentFarClip = MaxCamDistance;

				if (FarClip < 0)
					farLimit = -FarClip;
			}
			else
			{
				CurrentFarClip = FarClip;
			}

			CurrentFarClip += ClipRangeExcess;
			CurrentNearClip -= ClipRangeExcess;

			if (CurrentNearClip < nearLimit)
				CurrentNearClip = nearLimit;

			if (CurrentNearClip > farLimit)
				CurrentNearClip = farLimit;

			if (CurrentFarClip < nearLimit)
				CurrentFarClip = nearLimit;

			if (CurrentFarClip > farLimit)
				CurrentFarClip = farLimit;

			if (CurrentNearClip < CurrentFarClip * 1e-5)
				CurrentNearClip = CurrentFarClip * 1e-5;

			if (CurrentNearClip > CurrentFarClip * .9)
				CurrentNearClip = CurrentFarClip * .9;

			View = Microsoft.Xna.Framework.Matrix.CreateLookAt(Eye, LookAt, CameraUp);

			CurrentAspect = Aspect;
			if (CurrentAspect == 0)
				CurrentAspect = (float)PreferredBackBufferWidth / PreferredBackBufferHeight;
			Projection = Microsoft.Xna.Framework.Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians((float)Zoom), (float)CurrentAspect, (float)CurrentNearClip, (float)CurrentFarClip);

			ApplyChanges();
			MinCamDistance = 1e38;
			MaxCamDistance = -1e38;
			GraphicsDevice.Clear(ClearColor);

			GraphicsDevice.DepthStencilState = DepthStencilState.Default;
		}

		/// <summary>
		/// Returns a deepcopy of the texture
		/// </summary>
		/// <param name="tex">The texture to deepcopy</param>
		/// <returns>A deepcopy of tex</returns>
		public Texture2D CloneTexture2D(Texture2D tex)
		{
			if (BlDebug.ShowThreadWarnings && CreationThread != Thread.CurrentThread.ManagedThreadId)
				throw new Exception(String.Format("BlGraphicsDeviceManager.CloneTexture2D() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			var outTex = new Texture2D(GraphicsDevice, tex.Width, tex.Height);

			Color[] pixels = new Color[tex.Width * tex.Height];
			tex.GetData(pixels);
			outTex.SetData(pixels);

			return outTex;
		}

		public object Clone()
		{
			return MemberwiseClone();
		}


		~BlGraphicsDeviceManager()
		{
			throw new Exception("BlGraphicsDeviceManager was garbage collected before its Dispose was called");
		}

		int CreationThread = -1;
		/// <summary>
		/// Set when the object is Disposed.
		/// </summary>
		public bool IsDisposed = false;
		/// <summary>
		/// When finished with the object, you must call Dispose() from the same thread that created the object.
		/// You can call this multiple times, but once is enough.
		/// </summary>
		public new void Dispose()
		{
			if (BlDebug.ShowThreadInfo)
				Console.WriteLine("BlGraphicsDeviceManager dispose");

			if (IsDisposed)
				return;

			if (CreationThread != Thread.CurrentThread.ManagedThreadId)
				BlDebug.Message(String.Format("BlGraphicsDeviceManager.Dispose() was called by thread {0} instead of thread {1}", Thread.CurrentThread.ManagedThreadId, CreationThread));

			GC.SuppressFinalize(this);

			if (SpriteBatch != null)
			{
				SpriteBatch.Dispose();
			}

			base.Dispose();
			IsDisposed = true;

			if (BlDebug.ShowThreadInfo)
				Console.WriteLine("end BlGraphicsDeviceManager dispose");
		}
	}
}
