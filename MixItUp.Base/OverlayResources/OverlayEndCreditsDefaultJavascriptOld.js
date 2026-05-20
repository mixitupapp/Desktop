const animationKeyframes =
[
    { transform: "translateY(0vh)", offset: 0 },
    { transform: "translateY(-100%) translateY(-100vh)", offset: 1 },
];

const scrollRate = {ScrollRate};
const animationIterations = {AnimationIterations};
const fadeTime = 3000;

const main = document.getElementById("maindiv");
const list = document.getElementById("list");

const spacerTemplate = document.querySelector("#spacer");

var running = false;

function startCredits(data)
{
    if (running)
    {
        return;
    }
    running = true;

    while (list.firstChild) {
        list.removeChild(list.lastChild);
    }
    
    let totalElements = 0;

    data.Order.forEach((id) =>
    {
        let sectionTemplate = document.querySelector("#section-" + id);
        let section = sectionTemplate.content.cloneNode(true);
        
        let columnCount = data.Columns[id];
        let columns = section.firstElementChild.querySelector(".columns-" + id);
        let columnTemplate = document.querySelector("#column-" + id);
        for (let i = 0; i < columnCount; i++)
        {
            let column = columnTemplate.content.cloneNode(true);
            columns.appendChild(column.firstElementChild);
            
            totalElements++;
        }
        
        let columnIndex = 0;
        data.Items[id].forEach((item) =>
        {
            let lineTemplate = document.querySelector("#item-" + id);
            let line = lineTemplate.content.cloneNode(true);
            line.firstElementChild.innerHTML = item;
            columns.children[columnIndex].appendChild(line);
            
            totalElements++;
            
            columnIndex++;
            if (columnIndex >= columnCount)
            {
                columnIndex = 0;
            }
        });
        
        list.appendChild(section);
        list.appendChild(spacerTemplate.content.cloneNode(true));
    });
    
    main.style.display = "none";
    main.style.backgroundColor = "{BackgroundColor}";
    
    sendParentMessage({ Type: "EndCreditsStarted", ID: "{ID}" });
    $(main).fadeIn(fadeTime);
    
    setTimeout(() => {
        let rect = list.getBoundingClientRect();

        let animation = list.animate(animationKeyframes,
        {
            duration: (rect.bottom + document.documentElement.clientHeight) * scrollRate,
            easing: "linear",
            iterations: animationIterations,
        });

        animation.finished.then(() =>
        {
            $(main).fadeOut(fadeTime);
            
            while (list.firstChild) {
                list.removeChild(list.lastChild);
            }
            
            sendParentMessage({ Type: "EndCreditsCompleted", ID: "{ID}" });
            running = false;
        });
    }, fadeTime);
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });