(function(w,d){
    $(d).ready(async function () {
        const uriPrefix_GetImgApi = "file://app/archive/image/";
        const leayalobj = chrome.webview.hostObjects.leayal,
            jbody = $(d.body), 
            label_mangaName = $("#manga-name"),
            label_mangaAuthor = $("#manga-author"),
            label_mangaChapter = $("#manga-chapter"),
            imgList = d.querySelector('#content'),
            jpageSelector = $("#page-selector"),
            jimgList = $(imgList);

        const pageChangedCallback = function (e) {
            e.preventDefault();
            const pageNumber = jpageSelector.val();
            const element = d.querySelector("img.manga-page[page-number='" + pageNumber + "']");
            if (element) {
                element.scrollIntoView();
            }
        };

        let debounce_IntersectionObserver = 0;

        jpageSelector.on("change", pageChangedCallback);

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
                        const pageNumber = $(entry.target).attr("page-number");
                        jpageSelector.off("change", pageChangedCallback);
                        jpageSelector.val(pageNumber);
                        jpageSelector.on("change", pageChangedCallback);
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
                        const pageNumber = $(target).attr("page-number");
                        jpageSelector.off("change", pageChangedCallback);
                        jpageSelector.val(pageNumber);
                        jpageSelector.on("change", pageChangedCallback);
                    }
                }
            }, 100);
        }, {
            root: null,
            rootMargin: '0px',
            threshold: [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1]
        });

        var str = await leayalobj.OpenArchive();
        if (str) {
            var obj = JSON.parse(str);
            d.title = "Leayal Manga Viewer: " + obj.name;
            label_mangaName.text("Manga Name: " + obj.name);
            if (obj.author) {
                label_mangaAuthor.text("Author: " + obj.author).removeClass("hidden");
            } else {
                label_mangaAuthor.addClass("hidden");
            }
            if (obj.chapter) {
                label_mangaChapter.text("Chapter: " + obj.author).removeClass("hidden");
            } else {
                label_mangaChapter.addClass("hidden");
            }
            jbody.removeClass("no-archive");
            const pages = obj.images;
            if (pages) {
                jpageSelector.empty();
                for (const index in pages) {
                    const pageNumber = 1 + (((typeof(index) === "string") ? parseInt(index) : index) || 0);
                    const image = d.createElement("img");
                    jimgList.append($(image).addClass("manga-page").attr("src", uriPrefix_GetImgApi + pages[index]).attr("page-number", pageNumber));
                    observer.observe(image);
                    const option = document.createElement("option");
                    option.text = pageNumber;
                    option.value = pageNumber;
                    jpageSelector.append($(option)); 
                }
            }
        } else {
            jbody.addClass("no-archive");
        }
    });
})(window, window.document);