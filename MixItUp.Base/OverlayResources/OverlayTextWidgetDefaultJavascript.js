const text = document.getElementById("text");

function update(data)
{
    text.textContent = data.Text;
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });