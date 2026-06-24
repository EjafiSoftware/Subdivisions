import { ModRegistrar } from "cs2/modding";
import { SubdivisionsButton } from "mods/subdivisions-button";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append("GameTopLeft", SubdivisionsButton);
};

export default register;
