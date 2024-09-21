import { boundMask, pDescTooltip, pFocusKey, pMouseToolModule, pMouseToolTheme, pToolButtonModule, pToolButtonTheme, toolId, toolLocaleKey } from "constants"
import { bindValue, trigger, useValue } from "cs2/api"
import { tool } from "cs2/bindings"
import { useLocalization } from "cs2/l10n"
import { ModuleRegistry } from "cs2/modding"
import { Tooltip } from "cs2/ui";
import { useBindings as useBindings, simple2WayBinding, toggleSwitch, Active2WayBinding, useBinding } from "bindings"
import { ModToolSwitchKeys } from "../types/areabucket-types"

const couilStandard = "coui://uil/Standard/"
const areabucketUi = "coui://areabucket/"


const enable$ = bindValue(toolId, "AreaToolEnabled")
const useExperimentalOptions$ = bindValue<boolean>(toolId, "UseExperimentalOptions")

const toolActiveSwitch$ = simple2WayBinding<ModToolSwitchKeys>("ModActiveTool")

const simple2WayBindings = {
    // toolActiveSwitch: simple2WayBinding<ModToolSwitchKeys>("ModActiveTool"),
    fillRange: simple2WayBinding<number>("FillRange"),
    boundaryMask: simple2WayBinding<number>("BoundaryMask"),
    
    extraPoints: simple2WayBinding<boolean>("CheckBoundariesCrossing"),
    floodingDepth: simple2WayBinding<number>("RecursiveFloodingDepth")

}





export const AreaToolOptionsComponent = (moduleRegistry: ModuleRegistry) => (Component: any) => {
    // const { children, ...otherProps } = props || {};

    
    const ctx = {
        // translate: undefined as any, translateTool: undefined as any, translateToolDesc: undefined as any,
        ...getComponents(moduleRegistry)
    }

    const {
        components: { Section, ToolButton },
        themes: { toolButtonTheme, descTooltipTheme, mouseToolTheme },
        others: { focusKey },
        
    } = ctx

    

    const FocusDisabled = focusKey?.FOCUS_DISABLED

    const AreaBucketToolOptions = () => {


        const { translate } = useLocalization()
        const translateTool = (optionKey: string) => translate(toolLocaleKey + "." + optionKey)
        const translateToolDesc = (optionKey: string) => translate(toolLocaleKey + ".DESC." + optionKey)
        // ctx.translate = translate; ctx.translateTool = translateTool; ctx.translateToolDesc = translateToolDesc

        const useExpOptions = useValue(useExperimentalOptions$)
        const activeBindings = useBindings(simple2WayBindings)


        const fillRange = activeBindings.fillRange.value

        return (<>
            {
                useExpOptions &&
                <SingleRadio title={translateTool("DetectCrossing")}
                    src={couilStandard + "Jackhammer.svg"}
                    binding={activeBindings.extraPoints}
                    tooltip={translateToolDesc("DetectCrossing")}
                    {...ctx}
                />
            }
            <UpDown 
                title={translateTool("FillRange")}
                textValue= {fillRange + "m"}
                trigger={(n: number) => {
                    var scaling = 10
                    if (fillRange >= 50) scaling = 20
                    if (fillRange >= 150) scaling = 40
                    activeBindings.fillRange.trigger(fillRange + n * scaling)
                }}
                tooltip={translateToolDesc("FillRange")}
                {...ctx}
            />
            {
                useExpOptions && 
                <UpDown 
                title={translateTool("FloodingDepth")}
                textValue= {activeBindings.floodingDepth.value}
                trigger={(n: number) => {
                    activeBindings.floodingDepth.trigger(n + activeBindings.floodingDepth.value)
                }}
                tooltip={translateToolDesc("FloodingDepth")}
                {...ctx}
                />
            }
            <Section title={translateTool("BoundaryMask")}>
                <MaskCheckBox 
                    src={"Media/Tools/Net Tool/SimpleCurve.svg"}
                    binding={activeBindings.boundaryMask}
                    targetMask={boundMask.net}
                    tooltip={translateTool("MaskNet")}
                    {...ctx}
                />
                <MaskCheckBox 
                    src={couilStandard + "House.svg"}
                    binding={activeBindings.boundaryMask}
                    targetMask={boundMask.lot}
                    tooltip={translateTool("MaskLot")}
                    {...ctx}
                />
                <MaskCheckBox 
                    src={couilStandard + "Decals.svg"}
                    binding={activeBindings.boundaryMask}
                    targetMask={boundMask.area}
                    tooltip={translateTool("MaskArea")}
                    {...ctx}
                />
                <MaskCheckBox 
                    src={couilStandard + "Network.svg"}
                    binding={activeBindings.boundaryMask}
                    targetMask={boundMask.netlane}
                    tooltip={translateTool("MaskNetLane")}
                    {...ctx}
                />
                <MaskCheckBox
                    src={couilStandard + "DottedLinesMarkers.svg"}
                    binding={activeBindings.boundaryMask}
                    targetMask={boundMask.subnet}
                    tooltip={translateTool("MaskSubNet")}
                    {...ctx}
                />
            </Section>
        </>)
    }

    return (props: any) => {

        const { translate } = useLocalization()
        const translateTool = (optionKey: string) => translate(toolLocaleKey + "." + optionKey)
        const translateToolDesc = (optionKey: string) => translate(toolLocaleKey + ".DESC." + optionKey)
        // ctx.translate = translate; ctx.translateTool = translateTool; ctx.translateToolDesc = translateToolDesc
        
        const { children, ...otherProps } = props || {}

        const enable = useValue(enable$)


        // const TitledTooltip = (titleKey: string, descKey: string): JSX.Element => {
        //     return <>
        //         <div className={descTooltipTheme.title}>{translate(titleKey)}</div>
        //         <div className={descTooltipTheme.content}>{translate(descKey)}</div>
        //     </>
        // }


        var result = Component()
        const toolActiveSwitch = useBinding(toolActiveSwitch$)
        const isAreaBucketToolActivated = toolActiveSwitch.value === "AreaBucket"
        // if (!enable) return result // shows tool active radio buttons iff tool is enable
        

        // tool activation radio buttons
        // tool selection section title keeps original translation id "Active"
        if (enable) result.props.children?.push( 
            <Section title={translateTool("Active")}> 
                <MultiRadio 
                    title={translateTool("ActiveAreaBucket")}
                    src="Media/Tools/Zone Tool/FloodFill.svg"
                    binding={toolActiveSwitch}
                    defaultValue="Default"
                    targetValue="AreaBucket"
                    tooltip={translateToolDesc("ActiveAreaBucket")}
                    {...ctx}
                />
                <MultiRadio 
                    title={translateTool("ActiveAreaReplacement")}
                    src="Media/Tools/Net Tool/Replace.svg"
                    binding={toolActiveSwitch}
                    defaultValue="Default"
                    targetValue="AreaReplacement"
                    tooltip={translateToolDesc("ActiveAreaReplacement")}
                    {...ctx}
                />
            </Section>
        )

        result.props.children?.push(<>
            {enable && isAreaBucketToolActivated && <AreaBucketToolOptions/>}
        </>)


        return result
    }

    
}




const SingleRadio = (props: any) => {
    const { Section, ToolButton } = props.components
    const { toolButtonTheme } = props.themes
    const { focusKey } = props.others
    const FocusDisabled = focusKey?.FOCUS_DISABLED

    const binding = props.binding as Active2WayBinding<boolean>

    return (
        <Section title={props.title}>
            <ToolButton 
                className={toolButtonTheme.button}
                src={props.src}
                multiSelect={false}
                onSelect={() => toggleSwitch(binding)}
                selected={binding.value}
                disabled={false}
                focusKey={FocusDisabled}
                tooltip={props.tooltip}
            />
        </Section>
    )
}


const MultiRadio = (props: any) => {
    const { ToolButton } = props.components
    const { toolButtonTheme } = props.themes
    const { focusKey } = props.others

    const FocusDisabled = focusKey?.FOCUS_DISABLED

    const valueBinding = props.binding as Active2WayBinding<any>
    const targetValue = props.targetValue
    const defaultValue = props.defaultValue

    return (
        <ToolButton 
            className={toolButtonTheme.button}
            src={props.src}
            selected={valueBinding.value == targetValue}
            onSelect={() => {
                valueBinding.trigger(valueBinding.value === targetValue ? defaultValue : targetValue)
            }}
            multiSelect={false}
            focusKey={FocusDisabled}
            disabled={false}
            tooltip={props.tooltip}
        />
    )
}


const MaskCheckBox = (props: any) => {
    const { ToolButton } = props.components
    const { toolButtonTheme } = props.themes
    const { focusKey } = props.others

    const FocusDisabled = focusKey?.FOCUS_DISABLED

    const maskBinding = props.binding as Active2WayBinding<number>
    const targetMask = props.targetMask as number

    return (
        <ToolButton // area mask
            className={toolButtonTheme.button}
            src={props.src}
            selected={(maskBinding.value & targetMask) != 0}
            onSelect={() => maskBinding.trigger(maskBinding.value ^ targetMask)}
            multiSelect={true}
            focusKey={FocusDisabled}
            tooltip={props.tooltip}
        />
    )
}


const UpDown = (props: any) => {

    const { Section, ToolButton } = props.components
    const { mouseToolTheme } = props.themes
    const { focusKey } = props.others

    const FocusDisabled = focusKey?.FOCUS_DISABLED
    const localeId = props.localeId

    const triggerProxy = (v: number) => {
        const _trigger = props.trigger
        if (_trigger == undefined) return
        _trigger(v)
    }

    return (<Section title={props.title}>
        <ToolButton 
            className={mouseToolTheme.startButton}
            src={couilStandard + "ArrowDownThickStroke.svg"}
            selected={false}
            onSelect={() => triggerProxy(-1)}
            multiSelect={false}
            disabled={false}
            focusKey={FocusDisabled}
        />
        <Tooltip tooltip={props.tooltip}>
            <div className={mouseToolTheme.numberField}>{props.textValue}</div>
        </Tooltip>
        <ToolButton 
            className={mouseToolTheme.startButton}
            src={couilStandard + "ArrowUpThickStroke.svg"}
            selected={false}
            onSelect={() => triggerProxy(1)}
            multiSelect={false}
            disabled={false}
            focusKey={FocusDisabled}
        />
    </Section>)
}



const getComponents = (registry: ModuleRegistry) => {
    const mouseToolModule = registry.registry.get(pMouseToolModule)
    const toolButtonModule = registry.registry.get(pToolButtonModule)

    return {
        components: {
            Section: mouseToolModule?.Section,
            ToolButton: toolButtonModule?.ToolButton,
        },
        themes: {
            toolButtonTheme: registry.registry.get(pToolButtonTheme)?.classes,
            descTooltipTheme: registry.registry.get(pDescTooltip)?.classes,
            mouseToolTheme: registry.registry.get(pMouseToolTheme)?.classes,
        },
        others: {
            focusKey: registry.registry.get(pFocusKey)
        }
    }
}
