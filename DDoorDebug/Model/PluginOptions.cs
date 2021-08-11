using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DDoorDebug.Model
{
    public class PluginOptions
    {
        public bool menuEnabled;
        public bool hpEnabled = true;
        public bool sceneMenuEnabled;
        public bool freeCamEnabled;
        public bool freeCamMouse = true;
        public bool autoHeal;
        public bool velGraphEnabled;
        public readonly int maxGraphSamples = 20;
        public int velLineWidth = 1;
        public bool posHistGraphEnabled;
        public int maxPosHistSamles = 30;
        public readonly byte[] collViewMode = new byte[Enum.GetValues(typeof(ViewMode)).Length];
        public int cvmPos;
        public const float guiInfoWidth = 390;
        public const float graphWidth = 333;
        public const float graphHeight = 100;
        public readonly float frameSampleSize = 0.01666f;
        public readonly Vector2 graphPosGL = new Vector2(1920f-10f-guiInfoWidth-graphWidth-5, 1080f-10-graphHeight);
        public readonly Vector2 graphPosGUI = new Vector2(1920f-10f-guiInfoWidth-graphWidth-5, 10f);
        public MouseLook freeLookConf = new MouseLook(90f, 4f, 10f, 0.25f, 5f, 0, 0);

        public struct MouseLook
        {
            public readonly float cameraSensitivity;
		    public readonly float climbSpeed;
		    public readonly float normalMoveSpeed;
		    public readonly float slowMoveFactor;
		    public readonly float fastMoveFactor;
		    public float rotationX;
		    public float rotationY;

            public MouseLook(float cameraSensitivity, float climbSpeed, float normalMoveSpeed, float slowMoveFactor, float fastMoveFactor, float rotationX, float rotationY)
            {
                this.cameraSensitivity = cameraSensitivity;
                this.climbSpeed = climbSpeed;
                this.normalMoveSpeed = normalMoveSpeed;
                this.slowMoveFactor = slowMoveFactor;
                this.fastMoveFactor = fastMoveFactor;
                this.rotationX = rotationX;
                this.rotationY = rotationY;
            }
        }
        public enum ViewMode : int
        {
            Box = 0,
            Mesh = 1,
            Capsule = 2,
            Sphere = 3
        }
    }
}
