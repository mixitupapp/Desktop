var labels = document.getElementById("labels");

var displaySettings = "{DisplaySetting}";
var displayRotationSeconds = {DisplayRotationSeconds};
var displayRotationIndex = 0;

function rotateLabelDisplay()
{
    if (displayRotationIndex >= labels.children.length)
    {
        displayRotationIndex = 0;
    }
    
    for (const labelDisplay of labels.children) {
        labelDisplay.style.visibility = 'hidden';
    }
    
    let label = labels.children[displayRotationIndex];
    label.style.visibility = 'visible';
    
    displayRotationIndex++;
    
    setTimeout(() => { rotateLabelDisplay(); }, (displayRotationSeconds * 1000));
}

function add(data)
{
    let typeElement = document.getElementById(data.Type);
    if (typeElement == null) {
        let labelDisplayTemplate = document.getElementById("labeldisplay");
        const labelDisplay = labelDisplayTemplate.content.cloneNode(true);
        const labelText = labelDisplay.querySelector(".text");
        labelText.id = data.Type;
        labelText.innerHTML = data.Format;
        labelText.style.visibility = 'hidden';
        labels.appendChild(labelDisplay);
        
        if (labels.children.length == 1)
        {
            if (displaySettings == "RotatingDisplays")
            {
                rotateLabelDisplay();
            }
            else
            {
                labels.children[0].style.visibility = "visible";
            }
        }
    }
}

function update(data)
{
    let typeElement = document.getElementById(data.Type);
    if (typeElement != null) {
        typeElement.innerHTML = data.Format;
        
        if (displaySettings == "NewestOnly")
        {
            for (const labelDisplay of labels.children) {
                labelDisplay.style.visibility = 'hidden';
            }
            typeElement.style.visibility = 'visible';
        }
    }
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });