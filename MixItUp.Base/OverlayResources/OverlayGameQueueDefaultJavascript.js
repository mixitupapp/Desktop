const list = document.getElementById("list");

const itemTemplate = document.querySelector("#item");

var items = [];

function clear(data)
{
    items = [];
    while (list.childElementCount > 0)
    {
        list.removeChild(list.lastElementChild);
    }
}

function update(data)
{
    for (let i = 0; i < items.length && i < data.Items.length; i++)
    {
        if (items[i] !== data.Items[i].User.ID)
        {
            let oldItem = list.children[i];
            let newItem = createItem(data.Items[i]);
            performAnimation("{ItemRemovedAnimationFramework}", "{ItemRemovedAnimationName}", oldItem).then((result) =>
            {
                oldItem.replaceWith(newItem);
                addItem(newItem);
            });
        }
    }
    
    if (data.Items.length > items.length)
    {
        for (let i = items.length; i < data.Items.length; i++)
        {
            let item = createItem(data.Items[i]);
            list.appendChild(item);
            addItem(item);
        }
    }
    else if (items.length > data.Items.length)
    {
        for (let i = items.length - data.Items.length; i > 0; i--)
        {
            let oldItem = list.lastElementChild;
            performAnimation("{ItemRemovedAnimationFramework}", "{ItemRemovedAnimationName}", oldItem).then((result) =>
            {
                list.removeChild(oldItem);
            });
        }
    }
    
    items = [];
    for (let i = 0; i < data.Items.length; i++)
    {
        items.push(data.Items[i].User.ID);
    }
}

function createItem(itemData)
{
    let item = itemTemplate.content.cloneNode(true);
    
    let avatar = item.firstElementChild.querySelector(".avatar");
    avatar.src = itemData.User.AvatarLink;
    
    let text = item.firstElementChild.querySelector(".text");
    text.textContent = itemData.User.DisplayName;
    
    return item.firstElementChild;
}

function addItem(item)
{
    performAnimation("{ItemAddedAnimationFramework}", "{ItemAddedAnimationName}", item).then((result) =>
    {
        
    });
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });