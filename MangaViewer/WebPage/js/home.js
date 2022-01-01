(function (w, d) {

    const interop_func = (function () {
        const obj = Object.create(null);
        Object.defineProperty(obj, "tryAddFunc", {
            configurable: false,
            writable: false,
            enumerable: false,
            value: (function (functionName, func, isSealed = false) {
                if (!functionName || typeof (functionName) !== "string") {
                    console.error("tryAddFunc with non-string 'functionName' param");
                } else {
                    if (typeof (func) === "function") {
                        Object.defineProperty(obj, functionName, {
                            configurable: !!!isSealed,
                            enumerable: true,
                            writable: false,
                            value: func
                        });
                    } else {
                        console.error("tryAddFunc with non-function 'func' param");
                    }
                }
            }).bind(obj)
        });
        return obj;
    })();

    d.addEventListener("DOMContentLoaded", async function () {
        const leayalobj = w.chrome.webview.hostObjects.leayal,
            label_mangaName = d.getElementById("manga-name"),
            div_mangaAuthor = d.getElementById("div-manga-author"),
            label_mangaAuthor = d.getElementById("manga-author"),
            div_mangaChapter = d.getElementById("div-manga-chapter"),
            label_mangaChapter = d.getElementById("manga-chapter"),
            img_cover = d.getElementById("img-cover"),
            imgList = d.getElementById("content"),
            pageSelector = d.getElementById("page-selector");

        const uriPrefix_GetImgApi = await leayalobj.Endpoint_ArchiveGetImage;

        const pageChangedCallback = function (e) {
            e.preventDefault();
            const pageNumber = pageSelector.value;
            const element = d.querySelector("img.manga-page[page-number='" + pageNumber + "']");
            if (element) {
                element.scrollIntoView();
            }
        }, setPageNumberWithoutScrollToView = function (pageNumber) {
            pageSelector.removeEventListener("change", pageChangedCallback);
            pageSelector.value = pageNumber;
            pageSelector.addEventListener("change", pageChangedCallback);
        }, clearAllChildNodes = function (element) {
            if (element instanceof HTMLElement) {
                let child = element.firstChild;
                while (child) {
                    element.removeChild(child);
                    child = element.firstChild;
                }
            }
        };

        let debounce_IntersectionObserver = 0;

        pageSelector.addEventListener("change", pageChangedCallback);

        const observer = new IntersectionObserver((entries, observer) => {
            if (debounce_IntersectionObserver) {
                w.clearTimeout(debounce_IntersectionObserver);
                debounce_IntersectionObserver = 0;
            }
            debounce_IntersectionObserver = w.setTimeout(() => {
                if (debounce_IntersectionObserver) {
                    w.clearTimeout(debounce_IntersectionObserver);
                    debounce_IntersectionObserver = 0;
                }
                
                if (entries.length === 1) {
                    const entry = entries[0];
                    if (entry.isIntersecting) {
                        const pageNumber = entry.target.getAttribute("page-number") || 1;
                        setPageNumberWithoutScrollToView(pageNumber);
                    }
                } else {
                    let highest = 0, target = null;
                    for (const entry of entries) {
                        if (entry.intersectionRatio == 1) {
                            highest = entry.intersectionRatio;
                            target = entry.target;
                            break;
                        } else if (entry.intersectionRatio > highest) {
                            highest = entry.intersectionRatio;
                            target = entry.target;
                        }
                    }
                    if (target) {
                        const pageNumber = target.getAttribute("page-number") || 1;
                        setPageNumberWithoutScrollToView(pageNumber);
                    }
                }
            }, 100);
        }, {
            root: null,
            rootMargin: '0px',
            threshold: [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1]
        });

        interop_func.tryAddFunc("loadManga", async function () {
            var str = await leayalobj.OpenArchive();
            if (str) {
                var obj = JSON.parse(str);
                label_mangaName.textContent = d.title = obj.name;

                if (obj.author) {
                    label_mangaAuthor.textContent = obj.author;
                    div_mangaAuthor.classList.remove("hidden");
                } else {
                    div_mangaAuthor.classList.add("hidden");
                }
                if (obj.chapter) {
                    label_mangaChapter.textContent = obj.author;
                    div_mangaChapter.classList.remove("hidden");
                } else {
                    div_mangaChapter.classList.add("hidden");
                }

                let coverurl = obj.cover || "";

                const pages = obj.images;
                clearAllChildNodes(pageSelector);
                clearAllChildNodes(imgList);
                if (pages) {
                    for (const index in pages) {
                        const pageNumber = 1 + (((typeof (index) === "string") ? parseInt(index) : index) || 0);
                        const image = d.createElement("img");
                        image.classList.add("manga-page");
                        image.src = uriPrefix_GetImgApi + pages[index];

                        if (!coverurl && index == 0) {
                            coverurl = uriPrefix_GetImgApi + pages[index];
                        }

                        image.setAttribute("page-number", pageNumber);
                        imgList.appendChild(image);
                        observer.observe(image);
                        const option = document.createElement("option");
                        option.text = pageNumber;
                        option.value = pageNumber;
                        pageSelector.appendChild(option);
                    }
                }

                if (coverurl) {
                    img_cover.src = coverurl;
                    img_cover.classList.remove("hidden");
                } else {
                    img_cover.classList.add("hidden");
                }

                const classlist = d.body.classList;
                classlist.remove("loading");
                classlist.remove("no-archive");
                classlist.add("loaded");
            } else {
                const classlist = d.body.classList;
                classlist.remove("loading");
                classlist.remove("loaded");
                classlist.add("no-archive");
            }
        }, true);

        interop_func.tryAddFunc("setState", function (state_name) {
            if (state_name === "no-archive") {
                const classlist = d.body.classList;
                classlist.remove("loading");
                classlist.remove("loaded");
                classlist.add("no-archive");
                clearAllChildNodes(pageSelector);
                clearAllChildNodes(imgList);
                d.title = "";
            } else if (state_name === "loading") {
                const classlist = d.body.classList;
                classlist.remove("no-archive");
                classlist.remove("loaded");
                classlist.add("loading");
                d.title = "Loading";
                clearAllChildNodes(pageSelector);
                clearAllChildNodes(imgList);
            } else if (state_name === "loaded") {
                const classlist = d.body.classList;
                classlist.remove("loading");
                classlist.remove("no-archive");
                classlist.add("loaded");
            }
        }, true);

        w.chrome.webview.addEventListener("message", async function (arg) {
            if ("cmd" in arg.data) {
                const cmd = arg.data["cmd"];
                const args = ("args" in arg.data && Array.isArray(arg.data["args"]) ? arg.data["args"] : []);
                if (cmd in interop_func) {
                    const func = interop_func[cmd];
                    if (args.length === 0) {
                        func();
                    } else {
                        func.apply(this, args);
                    }
                }
                w.chrome.webview.postMessage(arg.data);
            }
        });

        // Call this here so that, in case user press F5/Refresh the webpage.
        // The browser will reload the manga after reloading the webpage.
        // In case the app has loaded a manga.
        interop_func.loadManga();

        w.chrome.webview.postMessage({
            event: "web-core-ready"
        });
    });
})(window, window.document);