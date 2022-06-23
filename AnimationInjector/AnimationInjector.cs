using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Tyto.Utilities
{
    [RequireComponent(typeof(Animator))]
    public class AnimationInjector : MonoBehaviour
    {
        [SerializeField] private AvatarMask fullBodyMask;

        private int currentAnimationIndex = 0;
        private int playableTransitionCount = 0;
        private Animator animator;
        private PlayableGraph playableGraph;
        private RuntimeAnimatorController animatorController;
        private AnimationLayerMixerPlayable layerMixerPlayable;

        private void Start()
        {
            animator = GetComponent<Animator>();

            if (fullBodyMask == null)
            {
                fullBodyMask = new AvatarMask();
                fullBodyMask.name = "GeneratedFullBodyMask";
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
                fullBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            }

            SetupPlayableGraph();
        }

        void OnDisable()
        {
            playableGraph.Destroy();
        }

        private void SetupPlayableGraph()
        {
            playableGraph = PlayableGraph.Create("AnimationGraph");
            layerMixerPlayable = AnimationLayerMixerPlayable.Create(playableGraph, 4);
            animatorController = animator.runtimeAnimatorController;
            var animatorControllerPlayable = AnimatorControllerPlayable.Create(playableGraph, animatorController);
            playableGraph.Connect(animatorControllerPlayable, 0, layerMixerPlayable, 0);
            layerMixerPlayable.SetInputWeight(0, 1);
            AnimationPlayableUtilities.Play(animator, layerMixerPlayable, playableGraph);
        }

        private void RemovePlayable(int index)
        {
            var playable = layerMixerPlayable.GetInput(index);
            layerMixerPlayable.DisconnectInput(index);
            layerMixerPlayable.SetInputWeight(index, 0);
            playable.Destroy();
        }

        private IEnumerator TransitAnimation(AnimationLayerMixerPlayable layerMixerPlayable, float time, int transitIndex)
        {
            var waitTime = Time.timeSinceLevelLoadAsDouble + time;
            var initialIndex = currentAnimationIndex;

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

        private IEnumerator StartAnimation(AnimationClip animationClip, AvatarMask avatarMask, float fadeTime)
        {
            var fromIndex = currentAnimationIndex;
            var toIndex = currentAnimationIndex == 3 ? 1 : currentAnimationIndex + 1;
            layerMixerPlayable.SetLayerMaskFromAvatarMask((uint)toIndex, avatarMask);

            if (layerMixerPlayable.GetInput(toIndex).IsValid()) RemovePlayable(toIndex);

            var temporaryPlayable = AnimationClipPlayable.Create(playableGraph, animationClip);
            layerMixerPlayable.ConnectInput(toIndex, temporaryPlayable, 0);
            currentAnimationIndex = toIndex;
            playableTransitionCount++;
            yield return TransitAnimation(layerMixerPlayable, fadeTime, toIndex);

            if (fromIndex != 0 && layerMixerPlayable.GetInput(fromIndex).IsValid()) RemovePlayable(fromIndex);
        }

        private IEnumerator EndAnimation(float fadeTime)
        {
            yield return TransitAnimation(layerMixerPlayable, fadeTime, 0);
            currentAnimationIndex = 0;
            playableTransitionCount++;

            for (int i = 1; i <= 3; i++)
                if (layerMixerPlayable.GetInput(i).IsValid()) RemovePlayable(i);
        }

        private IEnumerator StartAndEndAnimation(AnimationClip animationClip, AvatarMask avatarMask, float fadeInTime, float fadeOutTime)
        {
            if (fadeInTime + fadeOutTime > animationClip.length)
            {
                var surplus = fadeInTime + fadeOutTime - animationClip.length;
                var inRate = fadeInTime / (fadeInTime + fadeOutTime);
                var outRate = fadeOutTime / (fadeInTime + fadeOutTime);
                fadeInTime -= surplus * inRate;
                fadeOutTime -= surplus * outRate;
            }

            var localTransitionCount = playableTransitionCount + 1;
            yield return StartAnimation(animationClip, avatarMask, fadeInTime);
            if (playableTransitionCount != localTransitionCount) yield break;
            yield return new WaitForSeconds(animationClip.length - fadeInTime - fadeOutTime);
            if (playableTransitionCount != localTransitionCount) yield break;
            yield return EndAnimation(fadeOutTime);
        }

        public void SetAnimation(AnimationClip animationClip, AvatarMask avatarMask, float fadeTime)
        {
            StartCoroutine(StartAnimation(animationClip, avatarMask, fadeTime));
        }
        public void SetAnimation(AnimationClip animationClip, float fadeTime)
        {
            StartCoroutine(StartAnimation(animationClip, fullBodyMask, fadeTime));
        }

        public void ResetAnimation(float fadeTime)
        {
            StartCoroutine(EndAnimation(fadeTime));
        }

        public void PlayAnimation(AnimationClip animationClip, AvatarMask avatarMask, float fadeInTime, float fadeOutTime)
        {
            StartCoroutine(StartAndEndAnimation(animationClip, avatarMask, fadeInTime, fadeOutTime));
        }
        public void PlayAnimation(AnimationClip animationClip, float fadeInTime, float fadeOutTime)
        {
            StartCoroutine(StartAndEndAnimation(animationClip, fullBodyMask, fadeInTime, fadeOutTime));
        }
    }
}
