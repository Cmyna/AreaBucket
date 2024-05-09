import { ModRegistrar } from "cs2/modding";
import mod from "../mod.json";
import { AreaToolOptionsComponent } from "areaBucketToolOptions";
import * as areaToolVisible from './mods/areaToolOptionsVisible'
import { cMouseToolOptions, cUseToolOptionVisible } from "constants";

const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.extend(...cUseToolOptionVisible, areaToolVisible.ExtendedAreaToolVisible)
    moduleRegistry.extend(...cMouseToolOptions, AreaToolOptionsComponent(moduleRegistry))
    console.log(`${mod.id} tool UI module registration completed`)
}


export default register;