import { toolId } from "constants"
import { tool } from "cs2/bindings"
import { ModuleRegistryExtend } from "cs2/modding"



/**
 * @param Component useToolOptionsVisible function component, its type is `() => boolean`
 * @returns 
 */
export const ExtendedAreaToolVisible: ModuleRegistryExtend = (Component: any) => { 
    return () => {
        // use any to bypass api type restriction
        return Component() || [toolId, "Area Tool"].includes(tool.activeTool$.value.id)
    }
}



