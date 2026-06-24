import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { FloatingButton, Tooltip } from "cs2/ui";
import icon from "images/Subdivisions.svg";

const GROUP = "subdivisions";

const TOOLTIP_KEY = "Subdivisions.TOOLTIP_TOGGLE";
const TOOLTIP_FALLBACK = "Subdivisions - trace roads to enclose a district";

const active$ = bindValue<boolean>(GROUP, "active", false);

export const SubdivisionsButton = () => {
    const active = useValue(active$);
    const { translate } = useLocalization();

    return (
        <Tooltip tooltip={translate(TOOLTIP_KEY, TOOLTIP_FALLBACK)}>
            <FloatingButton
                src={icon}
                selected={active}
                onSelect={() => trigger(GROUP, "toggle")}
            />
        </Tooltip>
    );
};
