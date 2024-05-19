

import { toolId } from "constants"
import { bindValue, trigger, useValue } from "cs2/api"


type ExtractBindingType<T> = T extends TwoWayBindingDef<infer V> ? Active2WayBinding<V> : never


export type TwoWayBindingDef<V> = {
    use: () => void,
    trigger: (v: V) => void
}

export type Active2WayBinding<V> = {
    value: V,
    trigger: (v: V) => void
}


/**
 * init a simple 2 ways binding definition
 * @param propertyName the binding target, 
 * @returns a binding defintion, `use()` function in returned object behaves same as `useValue(propertyName)`, \
 * and trigger(value) invokes the game trigger binding `trigger(toolId, "Set" + propertyName, value)`
 */
export const simple2WayBinding = <V>(propertyName: string): TwoWayBindingDef<V> => {
    const valueBinding = bindValue<V>(toolId, propertyName)
    const res = {
        use: () => useValue(valueBinding),
        trigger: (v: any) => trigger(toolId, "Set" + propertyName, v)
    } as TwoWayBindingDef<V>
    return res
}


/**
 * the function can be call inside a function component like useValue, 
 * it is a helper function for calling all `use()` function for each binding definition object, and stores their returns to `value` property
 * @param bindings a dict stores 2 ways binding definitions, each one has `use()` and `trigger(value)` function
 * @returns a dict stores active bindings, which can access value property inside component
 */
export const useBindings = <T extends {[k: string]: TwoWayBindingDef<any>}>(bindings: T): { [k in keyof T]: ExtractBindingType<T[k]> } => {
    const res: any = {}
    Object.keys(bindings).forEach(k => {
        const binding = bindings[k] as TwoWayBindingDef<any>
        res[k] = {
            value: binding.use(),
            trigger: binding.trigger
        } as Active2WayBinding<any>
    })
    return res as any
}

/**
 * helper function just for boolean value 2-ways data binding
 * @param activeBinding 
 */
export const toggleSwitch = (activeBinding: Active2WayBinding<boolean>) => {
    activeBinding.trigger(!activeBinding.value)
}