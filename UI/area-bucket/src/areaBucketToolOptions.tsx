import { pDescTooltip, pFocusKey, pMouseToolModule, pMouseToolTheme, pToolButtonModule, pToolButtonTheme, toolId } from "constants"
import { bindValue, trigger, useValue } from "cs2/api"
import { tool } from "cs2/bindings"
import { useLocalization } from "cs2/l10n"
import { ModuleRegistry } from "cs2/modding"
import { Tooltip } from "cs2/ui";

export const triggerToolSwitch = () => trigger(toolId, "switch")

export const bucketToolEnabled$ = bindValue(toolId, "enabled")
export const log4Debug$ = bindValue(toolId, "log4debug")
export const fillRange$ = bindValue(toolId, "fillRange")
export const checkIntersection$ = bindValue(toolId, "checkIntersection")

export const AreaToolOptionsComponent = (moduleRegistry: ModuleRegistry) => (Component: any) => {
    // const { children, ...otherProps } = props || {};

    const ctx = getComponents(moduleRegistry)

    const {
        components: { Section, ToolButton },
        themes: { toolButtonTheme, descTooltipTheme, mouseToolTheme },
        others: { focusKey },
    } = ctx

    const FocusDisabled: any = focusKey?.FOCUS_DISABLED;

    return (props: any) => {

        const { translate } = useLocalization()
        const { children, ...otherProps } = props || {}

        const bucketToolEnabled = useValue(bucketToolEnabled$)
        const log4debug = useValue(log4Debug$)
        const fillRange = useValue(fillRange$)
        const checkIntersection = useValue(checkIntersection$)


        const TitledTooltip = (titleKey: string, descKey: string): JSX.Element => {
            return <>
                <div className={descTooltipTheme.title}>{translate(titleKey)}</div>
                <div className={descTooltipTheme.content}>{translate(descKey)}</div>
            </>
        }

        const showOption = [toolId, "Area Tool"].includes(tool.activeTool$.value.id)
        const bucketToolActive = tool.activeTool$.value.id === "Area Bucket"

        var result = Component()
        if (!showOption) return result;

        // switch button
        result.props.children?.push(
            <Section title={translate("AREABUCKET.ToolOptions.Title")}>
                <ToolButton 
                    className={toolButtonTheme.button}
                    src={"coui://uil/Standard/Dice.svg"}
                    tooltip={TitledTooltip("AREABUCKET.ToolOptions.Switch.TooltipTitle", "AREABUCKET.ToolOptions.Switch.TooltipDesc")}
                    multiSelect={false}
                    onSelect={() => trigger(toolId, "switch")}
                    selected={bucketToolEnabled}
                    disabled={false}
                    focusKey={FocusDisabled}
                />
            </Section>
        )

        if (!bucketToolActive) return result

        // result.props.children?.push(
        //     <Section title="Log for Debug">
        //         <ToolButton 
        //             className={toolButtonTheme.button}
        //             src={"coui://uil/Standard/MeasureEven.svg"}
        //             multiSelect={false}
        //             onSelect={() => trigger(toolId, "log4debugSwitch")}
        //             selected={log4debug}
        //             disabled={false}
        //             focusKey={FocusDisabled}
        //         />
        //     </Section>
        // )

        result.props.children?.push(
            <Section title="Check Intersection">
                <ToolButton 
                    className={toolButtonTheme.button}
                    src={"coui://uil/Standard/MeasureEven.svg"}
                    multiSelect={false}
                    onSelect={() => trigger(toolId, "checkIntersectionSwitch")}
                    selected={checkIntersection}
                    disabled={false}
                    focusKey={FocusDisabled}
                />
            </Section>
        )

        // tool control filling range
        if (bucketToolActive) result.props.children?.push(<UpDown 
            title="Fill Range" 
            textValue= {fillRange + "m"}
            trigger={(n: number) => trigger(toolId, "setFillRange", fillRange as number + n * 10)}
            {...ctx}
        />)
        

        return result
    }
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
            src="coui://uil/Standard/ArrowDownThickStroke.svg"
            selected={false}
            onSelect={() => triggerProxy(-1)}
            multiSelect={false}
            disabled={false}
            focusKey={FocusDisabled}
        />
        <Tooltip tooltip="test">
            <div className={mouseToolTheme.numberField}>{props.textValue}</div>
        </Tooltip>
        <ToolButton 
            className={mouseToolTheme.startButton}
            src="coui://uil/Standard/ArrowUpThickStroke.svg"
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
