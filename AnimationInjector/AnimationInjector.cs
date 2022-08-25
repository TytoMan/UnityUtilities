using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Tyto.Utilities
{
    [RequireComponent(typeof(Animator))]
    public class AnimationInjector : MonoBehaviour
    {
        [SerializeField] private AvatarMask _fullBodyMask;

        private int _currentAnimationIndex = 0;
        private int _playableTransitionCount = 0;
        private Animator _animator;
        private PlayableGraph _playableGraph;
        private RuntimeAnimatorController _animatorController;
        private AnimationLayerMixerPlayable _layerMixerPlayable;

        public int GetCurrentAnimationIndex => _currentAnimationIndex;
        public AnimationLayerMixerPlayable GetAnimationLayerMixerPlayable => _layerMixerPlayable;

        private void Start()
        {
            _animator = GetComponent<Animator>();

            if (_fullBodyMask == null)
            {
                _fullBodyMask = new AvatarMask();
                _fullBodyMask.name = "GeneratedFullBodyMask";
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
                _fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            }

            SetupPlayableGraph();
        }

        void OnDisable()
        {
            _playableGraph.Destroy();
        }

        private void SetupPlayableGraph()
        {
            _playableGraph = PlayableGraph.Create("AnimationGraph");
            _layerMixerPlayable = AnimationLayerMixerPlayable.Create(_playableGraph, 4);
            _animatorController = _animator.runtimeAnimatorController;
            var animatorControllerPlayable = AnimatorControllerPlayable.Create(_playableGraph, _animatorController);
            _playableGraph.Connect(animatorControllerPlayable, 0, _layerMixerPlayable, 0);
            _layerMixerPlayable.SetInputWeight(0, 1);
            AnimationPlayableUtilities.Play(_animator, _layerMixerPlayable, _playableGraph);
        }

        private void RemovePlayable(int index)
        {
            var playable = _layerMixerPlayable.GetInput(index);
            _layerMixerPlayable.DisconnectInput(index);
            _layerMixerPlayable.SetInputWeight(index, 0);
            playable.Destroy();
        }

        private IEnumerator TransitAnimation(AnimationLayerMixerPlayable layerMixerPlayable, float time, int transitIndex)
        {
            var waitTime = Time.timeSinceLevelLoadAsDouble + time;
            var initialIndex = _currentAnimationIndex;

            var initialWeight = Vector3.zero;
            initialWeight.x = layerMixerPlayable.GetInputWeight(1);
            initialWeight.y = layerMixerPlayable.GetInputWeight(2);
            initialWeight.z = layerMixerPlayable.GetInputWeight(3);

            var targetWeight = Vector3.zero;
            switch (transitIndex)
            {
                case 1: targetWeight.x = 1; break;
                case 2: targetWeight.y = 1; break;
                case 3: targetWeight.z = 1; break;
            }

            layerMixerPlayable.SetInputWeight(0, 1);

            yield return new WaitWhile(() =>
            {
                if (_currentAnimationIndex != initialIndex) return false;

                var diff = waitTime - Time.timeSinceLevelLoadAsDouble;
                if (diff > 0)
                {
                    var weight = Vector3.Lerp(targetWeight, initialWeight, (float)diff / time);
                    if (!layerMixerPlayable.GetInput(1).IsValid()) weight.x = 0;
                    if (!layerMixerPlayable.GetInput(2).IsValid()) weight.y = 0;
                    if (!layerMixerPlayable.GetInput(3).IsValid()) weight.z = 0;

                    layerMixerPlayable.SetInputWeight(1, weight.x);
                    layerMixerPlayable.SetInputWeight(2, weight.y);
                    layerMixerPlayable.SetInputWeight(3, weight.z);

                    return true;
                }
                else
                {
                    layerMixerPlayable.SetInputWeight(1, targetWeight.x);
                    layerMixerPlayable.SetInputWeight(2, targetWeight.y);
                    layerMixerPlayable.SetInputWeight(3, targetWeight.z);

                    return false;
                }
            });
        }

        private IEnumerator StartAnimation(AnimationClip animationClip, float fadeTime, AvatarMask avatarMask)
        {
            var fromIndex = _currentAnimationIndex;
            var toIndex = _currentAnimationIndex == 3 ? 1 : _currentAnimationIndex + 1;
            _layerMixerPlayable.SetLayerMaskFromAvatarMask((uint)toIndex, avatarMask);

            if (_layerMixerPlayable.GetInput(toIndex).IsValid()) RemovePlayable(toIndex);

            var temporaryPlayable = AnimationClipPlayable.Create(_playableGraph, animationClip);
            _layerMixerPlayable.ConnectInput(toIndex, temporaryPlayable, 0);
            _currentAnimationIndex = toIndex;
            _playableTransitionCount++;
            yield return TransitAnimation(_layerMixerPlayable, fadeTime, toIndex);

            if (fromIndex != 0 && _layerMixerPlayable.GetInput(fromIndex).IsValid()) RemovePlayable(fromIndex);
        }

        private IEnumerator EndAnimation(float fadeTime)
        {
            var localTransitionCount = _playableTransitionCount;
            _currentAnimationIndex = 0;
            yield return TransitAnimation(_layerMixerPlayable, fadeTime, 0);
            if (_playableTransitionCount != localTransitionCount) yield break;

            for (int i = 1; i <= 3; i++)
                if (_layerMixerPlayable.GetInput(i).IsValid()) RemovePlayable(i);
        }

        private IEnumerator StartAndEndAnimation(AnimationClip animationClip, float fadeInTime, float fadeOutTime, AvatarMask avatarMask)
        {
            if (fadeInTime + fadeOutTime > animationClip.length)
            {
                var surplus = fadeInTime + fadeOutTime - animationClip.length;
                var inRate = fadeInTime / (fadeInTime + fadeOutTime);
                var outRate = fadeOutTime / (fadeInTime + fadeOutTime);
                fadeInTime -= surplus * inRate;
                fadeOutTime -= surplus * outRate;
            }

            var localTransitionCount = _playableTransitionCount + 1;
            yield return StartAnimation(animationClip, fadeInTime, avatarMask);
            if (_playableTransitionCount != localTransitionCount) yield break;
            yield return new WaitForSeconds(animationClip.length - fadeInTime - fadeOutTime);
            if (_playableTransitionCount != localTransitionCount) yield break;
            yield return EndAnimation(fadeOutTime);
        }

        public void SetAnimation(AnimationClip animationClip, float fadeTime, AvatarMask avatarMask)
        {
            StartCoroutine(StartAnimation(animationClip, fadeTime, avatarMask));
        }
        public void SetAnimation(AnimationClip animationClip, float fadeTime)
        {
            StartCoroutine(StartAnimation(animationClip, fadeTime, _fullBodyMask));
        }

        public void ResetAnimation(float fadeTime)
        {
            StartCoroutine(EndAnimation(fadeTime));
        }

        public void PlayAnimation(AnimationClip animationClip, float fadeInTime, float fadeOutTime, AvatarMask avatarMask)
        {
            StartCoroutine(StartAndEndAnimation(animationClip, fadeInTime, fadeOutTime, avatarMask));
        }
        public void PlayAnimation(AnimationClip animationClip, float fadeInTime, float fadeOutTime)
        {
            StartCoroutine(StartAndEndAnimation(animationClip, fadeInTime, fadeOutTime, _fullBodyMask));
        }
    }
}