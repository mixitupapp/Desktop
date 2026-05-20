var labels = document.getElementById("labels");

var displaySettings = "{DisplaySetting}";
var displayRotationSeconds = {DisplayRotationSeconds};
var displayRotationIndex = 0;

function addLabelDisplay(type, format)
{
    let labelDisplayTemplate = document.getElementById("labeldisplay");
    const labelDisplay = labelDisplayTemplate.content.cloneNode(true);
    const labelText = labelDisplay.querySelector(".text");
    labelText.id = type;
    labelText.innerHTML = format;
    labelText.style.visibility = 'hidden';
    labels.appendChild(labelDisplay);
}

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

{LabelAdds}

if (displaySettings == "RotatingDisplays")
{
    rotateLabelDisplay();
}
else
{
    labels.children[0].style.visibility = "visible";
}