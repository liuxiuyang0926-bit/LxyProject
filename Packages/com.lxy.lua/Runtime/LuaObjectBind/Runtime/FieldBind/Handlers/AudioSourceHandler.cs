using UnityEngine;

namespace LuaObjectBind.Handlers
{
    [FieldBindHandler]
    public class AudioSourceHandler : AFieldBindHandler<AudioSource>
    {
        protected override void HandleFloat(AudioSource comp, FieldBindEnum type, float value)
        {
            switch (type)
            {
                case FieldBindEnum.AudioSource_Volume:
                    comp.volume = value;
                    break;
            }
        }

        protected override float GetFloat(AudioSource comp, FieldBindEnum type)
        {
            switch (type)
            {
                case FieldBindEnum.AudioSource_Volume:
                    return comp.volume;
            }

            return 0;
        }

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