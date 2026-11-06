using UnityEngine.Timeline;

/// <summary>컷씬 카메라 경로 트랙 — CutsceneCameraPath(카메라에 붙음)에 바인딩.</summary>
[TrackColor(0.4f, 0.7f, 0.95f)]
[TrackClipType(typeof(CameraPathClip))]
[TrackBindingType(typeof(CutsceneCameraPath))]
public class CameraPathTrack : TrackAsset
{
}
