import { boundMask, pDescTooltip, pFocusKey, pMouseToolModule, pMouseToolTheme, pToolButtonModule, pToolButtonTheme, toolId, toolLocaleKey } from "constants"
import { bindValue, trigger, useValue } from "cs2/api"
import { tool } from "cs2/bindings"
import { useLocalization } from "cs2/l10n"
import { ModuleRegistry } from "cs2/modding"
import { Tooltip } from "cs2/ui";
import { useBindings as useBindings, simple2WayBinding, toggleSwitch, Active2WayBinding } from "bindings"

const couiStandard = "coui://uil/Standard/"
const areabucketUi = "coui://areabucket/"


const enable$ = bindValue(toolId, "ToolEnabled")
const useExperimentalOptions$ = bindValue<boolean>(toolId, "UseExperimentalOptions")




const simple2WayBindings = {
    //showDebug: simple2WayBinding<boolean>("ShowDebugOptions"),
    //log4Debug: simple2WayBinding<boolean>("Log4Debug"),
    active: simple2WayBinding<boolean>("Active"),
    fillRange: simple2WayBinding<number>("FillRange"),
    boundaryMask: simple2WayBinding<number>("BoundaryMask"),
    
    //extraPoints: simple2WayBinding<boolean>("ExtraPoints"),
    floodingDepth: simple2WayBinding<number>("RecursiveFloodingDepth")
}


export const AreaToolOptionsComponent = (moduleRegistry: ModuleRegistry) => (Component: any) => {
    // const { children, ...otherProps } = props || {};

    const ctx = {
        translate: undefined as any, translateTool: undefined as any, translateToolDesc: undefined as any,
        ...getComponents(moduleRegistry)
    }

    const {
        components: { Section, ToolButton },
        themes: { toolButtonTheme, descTooltipTheme, mouseToolTheme },
        others: { focusKey },
        
    } = ctx

    const FocusDisabled = focusKey?.FOCUS_DISABLED

    return (props: any) => {

        const { translate } = useLocalization()
        const translateTool = (optionKey: string) => translate(toolLocaleKey + "." + optionKey)
        const translateToolDesc = (optionKey: string) => translate(toolLocaleKey + ".DESC." + optionKey)
        ctx.translate = translate; ctx.translateTool = translateTool; ctx.translateToolDesc = translateToolDesc
        const { children, ...otherProps } = props || {}

        const enable = useValue(enable$)
        const useExpOptions = useValue(useExperimentalOptions$)

        
        const activeBindings = useBindings(simple2WayBindings)


        const TitledTooltip = (titleKey: string, descKey: string): JSX.Element => {
            return <>
                <div className={descTooltipTheme.title}>{translate(titleKey)}</div>
                <div className={descTooltipTheme.content}>{translate(descKey)}</div>
            </>
        }


        var result = Component()
        
        if (!enable) return result // shows switch button iff tool is enable
        

        // switch button
        result.props.children?.push(
            <Radio title={translateTool("Active")}
                src="Media/Tools/Zone Tool/FloodFill.svg"
                binding={activeBindings.active}
                {...ctx}
            />
        )
        

        if (!activeBindings.active.value) return result // shows tool options iff it is active

        // if (useExpOptions) result.props.children?.push(
        // <Radio title={translateTool("DetectCrossing")}
        //     src={couiStandard + "Jackhammer.svg"}
        //     binding={activeBindings.extraPoints}
        //     tooltip={translateToolDesc("DetectCrossing")}
        //     {...ctx}
        // />)


        // tool control filling range
        const fillRange = activeBindings.fillRange.value
        result.props.children?.push(<>
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
        </>)


        // tool control boundary mask
        // const boundaryMask = activeBindings.boundaryMask.value
        result.props.children?.push(<Section title={translateTool("BoundaryMask")}>
            <MaskCheckBox 
                src={"Media/Tools/Net Tool/SimpleCurve.svg"}
                binding={activeBindings.boundaryMask}
                targetMask={boundMask.net}
                tooltip={translateTool("MaskNet")}
                {...ctx}
            />
            <MaskCheckBox 
                src={couiStandard + "House.svg"}
                binding={activeBindings.boundaryMask}
                targetMask={boundMask.lot}
                tooltip={translateTool("MaskLot")}
                {...ctx}
            />
            <MaskCheckBox 
                src={couiStandard + "Decals.svg"}
                binding={activeBindings.boundaryMask}
                targetMask={boundMask.area}
                tooltip={translateTool("MaskArea")}
                {...ctx}
            />

            {
                //useExpOptions && 
                (<>
                    <MaskCheckBox 
                        src={couiStandard + "Network.svg"}
                        binding={activeBindings.boundaryMask}
                        targetMask={boundMask.netlane}
                        tooltip={translateTool("MaskNetLane")}
                        {...ctx}
                    />
                    <MaskCheckBox
                        src={couiStandard + "DottedLinesMarkers.svg"}
                        binding={activeBindings.boundaryMask}
                        targetMask={boundMask.subnet}
                        tooltip={translateTool("MaskSubNet")}
                        {...ctx}
                    />
                </>)
            }
        </Section>)


        return result
    }
}


const Radio = (props: any) => {
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
            src={couiStandard + "ArrowDownThickStroke.svg"}
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
            src={couiStandard + "ArrowUpThickStroke.svg"}
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
