using Game.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaBucket.Utils
{
    public static class BindingUtils
    {
        private static Action<ProxyBinding, ProxyBinding> _setMimic = (mimicBinding, targetBinding) =>
        {
            // I am not sure why needs a copy? the ProxyBinding is a struct so all fields in it should been 'copied' into stack
            var newMimicBinding = mimicBinding.Copy();
            newMimicBinding.path = targetBinding.path;
            newMimicBinding.modifiers = targetBinding.modifiers;
            InputManager.instance.SetBinding(newMimicBinding, out _);
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mimicAction">the mimic action</param>
        /// <param name="targetActionMap">map name of source action to be mimiced</param>
        /// <param name="targetActionname">name of soruce action to be mimiced</param>
        /// <param name="group">what binding group belongs to (related to property ProxyBinding.group)</param>
        /// <returns></returns>
        public static bool MimicBuiltinBinding(ProxyAction mimicAction, string targetActionMap, string targetActionname, string group)
        {
            var targetAction = InputManager.instance.FindAction(targetActionMap, targetActionname);
            if (targetAction == null) return false;

            // get binding from action in same group
            var builtinBindng = targetAction.bindings.FirstOrDefault(b => b.group == group);
            var mimicBinding = mimicAction.bindings.FirstOrDefault(b => b.group == group);

            // setting watcher that auto update the source key binding to mimic bindng once the source binding changes
            var applyWatcher = new ProxyBinding.Watcher(builtinBindng, binding => _setMimic(mimicBinding, binding));
            _setMimic(mimicBinding, builtinBindng); // apply it once immediately
            return true;
        }
    }
}
