using UnityEngine;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class AudioSourceHandler : AFieldBindHandler<AudioSource>
    {
        /// <summary>
        /// 将浮点值写入音频组件。
        /// </summary>
        protected override void HandleFloat(AudioSource comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.AudioSource_Volume:
                    comp.volume = value;
                    break;
            }
        }

        /// <summary>
        /// 获取浮点数。
        /// </summary>
        protected override float GetFloat(AudioSource comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.AudioSource_Volume:
                    return comp.volume;
            }

            return 0;
        }

        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected override void HandleBool(AudioSource comp, FieldBindEnum type, bool value)
        {
            switch (type)
            {
                case FieldBindEnum.AudioSource_Mute:
                    comp.mute = value;
                    break;
                case FieldBindEnum.AudioSource_PlayOnAwake:
                    comp.playOnAwake = value;
                    break;
                case FieldBindEnum.AudioSource_Loop:
                    comp.loop = value;
                    break;
            }
        }

        /// <summary>
        /// 获取布尔值。
        /// </summary>
        protected override bool GetBool(AudioSource comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.AudioSource_Mute:
                    return comp.mute;
                case FieldBindEnum.AudioSource_PlayOnAwake:
                    return comp.playOnAwake;
                case FieldBindEnum.AudioSource_Loop:
                    return comp.loop;
            }

            return false;
        }
    }
} 