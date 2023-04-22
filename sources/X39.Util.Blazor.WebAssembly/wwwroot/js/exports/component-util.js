export function getElementBounds(element) {
    var bounding = element.getBoundingClientRect();
    return {
        top: bounding.top,
        left: bounding.left,
        width: bounding.width,
        height: bounding.height,
    };
}

//@region ResizeObserver

let resizeObserverMap = new Map();
let resizeObserver = new ResizeObserver(entries => {
    for (let entry of entries) {
        var obj = resizeObserverMap.get(entry.target)
        let rect = {
            top: entry.contentRect.top,
            left: entry.contentRect.left,
            width: entry.contentRect.width,
            height: entry.contentRect.height,
        };
        obj.dotNetObject.invokeMethodAsync("OnSizeChange", obj.guid, rect);
    }
});

export function registerOnSizeChange(element, guid, dotNetObject) {
    resizeObserverMap.set(element, {guid: guid, dotNetObject: dotNetObject});
    resizeObserver.observe(element);
}

export function unregisterOnSizeChange(element) {
    resizeObserverMap.delete(element);
    resizeObserver.unobserve(element);
}

//@endregion

//@region Window Events

let windowEventMap = new Map();

export function registerWindowEvent(event, guid, dotNetObject) {
    let func = (e) => {
        const obj = windowEventMap.get(event);
        let data = {};
        for (let key in e) {
            if (typeof(e[key]) === "number" || typeof(e[key]) === "string" || e[key] === "boolean" || e[key] === "bigint") {
                data[key] = e[key];
            }
        }
        obj.dotNetObject.invokeMethodAsync("OnWindowEvent", obj.guid, data);
    };
    windowEventMap.set(event, {guid: guid, dotNetObject: dotNetObject, func: func});
    window.addEventListener(event, func);
}

export function unregisterWindowEvent(event) {
    let obj = windowEventMap.get(event);
    windowEventMap.delete(event);
    window.removeEventListener(event, obj.func);
}
        

//@endregion
