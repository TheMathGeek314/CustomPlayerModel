using System.Linq;
using UnityEngine;

namespace CustomPlayerModel {
    public class AnimationStateController : MonoBehaviour {
        Animator animator;
        public GameObject parent;
        string currentState = "";
        private customPropertyType customProperty = customPropertyType.None;
        public float nonSwimHeight;

        public static AnimationStateController instance;
        static string[] implementedStates = { };

        void Start() {
            instance = this;
            animator = GetComponent<Animator>();
            implementedStates = new string[animator.runtimeAnimatorController.animationClips.Length];
            for(int i = 0; i < implementedStates.Length; i++) {
                implementedStates[i] = animator.runtimeAnimatorController.animationClips[i].name;
            }
        }

        void Update() {}

        public static void updateState(string newState) {
            if(instance == null)
                return;
            newState = filterStates(newState);
            if(newState == "")
                return;
            if(newState == instance.currentState)
                return;
            if(!implementedStates.Contains(newState)) {
                //Modding.Logger.Log($"[CustomPlayerModel] - Unhandled Clip \"{newState}\"");
                return;
            }
            updateCustomProperty(newState);
            instance.animator.SetBool(newState, true);
            instance.animator.SetBool(instance.currentState, false);
            instance.currentState = newState;
        }

        private static string filterStates(string newState) {
            switch(newState) {
                case "":                    return "";
                case "SlashEffect":         return "";
                case "SlashEffectAlt":      return "";
                case "UpSlash":             return "Slash";
                case "DownSlash":           return "Slash";
                case "UpSlashEffect":       return "";
                case "DownSlashEffect":     return "";
                case "SD Crys Grow":        return "";
                case "Death Head Normal":   return "";
                case "Death Head Cracked":  return "";
                case "Lantern Idle":        return "Idle";
                case "Acid Death":          return "Death";
                case "Spike Death":         return "Death";
                case "Hit Crack Appear":    return "";
                case "DN Cancel":           return "";
                case "Collect Normal 2":    return "Collect Normal 1";
                case "Collect Normal 3":    return "Collect Normal 1";
                case "NA Charge":           return "";
                case "Sit Lean":            return "";
                case "Sitting Asleep":      return "";
                case "Run To Idle":         return "";
                case "Focus Get":           return "";
                case "Focus End":           return "";
                case "Focus Get Once":      return "";
                case "Idle Hurt":           return "Idle";
                case "Fireball Antic":      return "";
                case "SD Crys Idle":        return "";
                case "Sit Map Close":       return "Sit Idle";
                case "Dash To Idle":        return "";
                case "Dash Effect":         return "";
                case "Lantern Run":         return "Run";
                case "SD Fx Charge":        return "";
                case "SD Charge Ground End":return "";
                case "SD Fx Bling":         return "";
                case "SD Fx Burst":         return "";
                case "Double Jump Wings 2": return "Double Jump";
                case "Fireball2 Cast":      return "Fireball1 Cast";
                case "NA Big Slash Effect": return "";
                case "NA Charged Effect":   return "";
                case "Scream End 2":        return "";
                case "Scream End":          return "";
                case "Scream Start":        return "";
                case "Scream 2":            return "Scream";
                case "Shadow Dash":         return "Dash";
                case "Shadow Dash Burst":   return "";
                case "Shadow Recharge":     return "";
                case "Wall Slash":          return "Slash";
                case "Cyclone Effect":      return "";
                case "Cyclone Effect End":  return "";
                case "Surface In":          return "Surface Idle";
                case "LookUpToIdle":        return "";
                case "DN Slash Antic":      return "";
                case "DN Slash":            return "";
                case "NA Dash Slash Effect":return "";
                case "UpSlashEffect M":     return "";
                case "DownSlashEffect M":   return "";
                case "SlashEffect M":       return "";
                case "SlashEffectAlt M":    return "";
                case "SlashEffect F":       return "";
                case "SlashEffectAlt F":    return "";
                case "UpSlashEffect F":     return "";
                case "DownSlashEffect F":   return "";
                case "Dream Death":         return "Death";
                case "SD Crys Flash":       return "";
                case "SD Crys Shrink":      return "";
                case "Surface InToIdle":    return "";
                case "Surface InToSwim":    return "";
                case "Spike Death Antic":   return "";
                default: return newState;
            }
        }

        private static void updateCustomProperty(string newState) {
            customPropertyType property;
            switch(newState) {
                case "Sit":
                case "Sit Lean":
                case "Sitting Asleep":
                case "Sit Fall Asleep":
                case "Sit Map Open":
                case "Wake To Sit":
                case "Sit Map Close":
                case "Scream":
                case "Thorn Attack":
                case "Sit Idle":
                    property = customPropertyType.FaceFront;
                    break;
                case "Wall Slide":
                    property = customPropertyType.HorizontalFlip;
                    break;
                case "Surface Idle":
                case "Surface In":
                case "Surface InToIdle":
                case "Surface InToSwim":
                    property = customPropertyType.SwimHeight;
                    break;
                default:
                    property = customPropertyType.None;
                    break;
            }
            if(property == instance.customProperty)
                return;
            instance.customProperty = property;
            float xScale = property == customPropertyType.FaceFront ? 1 : 0.1f;
            float zScale = property == customPropertyType.FaceFront ? 0.1f : 1;
            float invertX = instance.parent.transform.localScale.x > 0 ? 1 : -1;
            instance.parent.transform.localScale = new Vector3(xScale * invertX, 1, zScale);
            instance.parent.transform.rotation = Quaternion.Euler(0, (property == customPropertyType.FaceFront ? 180 : (HeroController.instance.cState.facingRight ? 90 : 270)) + (property == customPropertyType.HorizontalFlip ? 180 : 0), 0);
            instance.parent.transform.localPosition = new Vector3(0, property == customPropertyType.SwimHeight ? -1.85f : instance.nonSwimHeight, property == customPropertyType.FaceFront ? -0.3f : 0);
        }

        private enum customPropertyType {
            FaceFront,
            HorizontalFlip,
            SwimHeight,
            None
        }
    }
}
