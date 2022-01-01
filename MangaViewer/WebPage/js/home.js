(function (w, d) {
    d.addEventListener("DOMContentLoaded", async function () {
        d.title = "";
        const leayalobj = w.chrome.webview.hostObjects.leayal,
            label_mangaName = d.getElementById("manga-name"),
            label_mangaAuthor = d.getElementById("manga-author"),
            label_mangaChapter = d.getElementById("manga-chapter"),
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

        const func_loadManga = async function () {
            var str = await leayalobj.OpenArchive();
            if (str) {
                var obj = JSON.parse(str);
                d.title = obj.name;
                label_mangaName.textContent = "Manga Name: " + obj.name;
                if (obj.author) {
                    label_mangaAuthor.textContent = "Author: " + obj.author;
                    label_mangaAuthor.classList.remove("hidden");
                } else {
                    label_mangaAuthor.classList.add("hidden");
                }
                if (obj.chapter) {
                    label_mangaChapter.textContent = "Author: " + obj.author;
                    label_mangaChapter.classList.remove("hidden");
                } else {
                    label_mangaChapter.classList.add("hidden");
                }
                const pages = obj.images;
                clearAllChildNodes(pageSelector);
                clearAllChildNodes(imgList);
                if (pages) {
                    for (const index in pages) {
                        const pageNumber = 1 + (((typeof (index) === "string") ? parseInt(index) : index) || 0);
                        const image = d.createElement("img");
                        image.classList.add("manga-page");
                        image.src = uriPrefix_GetImgApi + pages[index];
                        image.setAttribute("page-number", pageNumber);
                        imgList.appendChild(image);
                        observer.observe(image);
                        const option = document.createElement("option");
                        option.text = pageNumber;
                        option.value = pageNumber;
                        pageSelector.appendChild(option);
                    }
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
        }

        w.chrome.webview.addEventListener("message", async function (arg) {
            if ("cmd" in arg.data) {
                const cmd = arg.data["cmd"];
                const args = ("args" in arg.data && Array.isArray(arg.data["args"]) ? arg.data["args"] : []);
                if (cmd === "setState") {
                    const state_name = args[0];
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
                } else if (cmd === "loadManga") {
                    await func_loadManga();
                }
                w.chrome.webview.postMessage(arg.data);
            }
        });
    });
})(window, window.document);