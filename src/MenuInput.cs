using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace SludgeLife2VR {
    // One relative cursor feeds uGUI and native mouse actions. It never depends on controller aim.
    public sealed class VrUiInputModule : PointerInputModule {
        sealed class Slot {internal PointerEventData Data;internal GameObject Pressed,Legacy,Drag,LastClick;internal float ClickTime;}
        readonly Slot primary=new Slot(),secondary=new Slot();
        GameObject hover;
        Button focus;
        bool initialized;
        Vector2 last;
        public override bool ShouldActivateModule(){return VrPlugin.Active && VrPlugin.Instance.UI.MenuOpen;}
        public override void Process() {
            if(!ShouldActivateModule())return;
            UiBridge ui=VrPlugin.Instance.UI;VrPlugin.Instance.Mouse.Poll();
            Vector2 point=ui.MousePoint();
            if(focus==null || !focus.IsActive() || !focus.IsInteractable())focus=ui.FirstButton();
            Vector2 nav=Input.GetKeyDown(KeyCode.UpArrow)?Vector2.up:Input.GetKeyDown(KeyCode.DownArrow)?Vector2.down:Input.GetKeyDown(KeyCode.LeftArrow)?Vector2.left:Input.GetKeyDown(KeyCode.RightArrow)?Vector2.right:Vector2.zero;
            if(nav.sqrMagnitude>0){focus=ui.Navigate(focus,nav);if(focus!=null)ui.FocusCursor(focus);point=ui.MousePoint();}
            Vector2 delta=initialized?point-last:Vector2.zero;last=point;initialized=true;
            GameObject target=ui.HitAt(point);
            ProcessSlot(primary,target,point,delta,0,VirtualMouse.Down(0)||Input.GetKeyDown(KeyCode.Return),VirtualMouse.Up(0)||Input.GetKeyUp(KeyCode.Return),VirtualMouse.Held(0)||Input.GetKey(KeyCode.Return));
            ProcessSlot(secondary,target,point,delta,1,VirtualMouse.Down(1),VirtualMouse.Up(1),VirtualMouse.Held(1));
            Vector2 scroll=VirtualMouse.Scroll();if(scroll.sqrMagnitude>0){primary.Data.scrollDelta=scroll;ExecuteEvents.ExecuteHierarchy(target,primary.Data,ExecuteEvents.scrollHandler);}
            ui.ShowCursor(point,true);
        }
        void ProcessSlot(Slot slot,GameObject target,Vector2 point,Vector2 delta,int button,bool down,bool up,bool held) {
            if(slot.Data==null){PointerEventData p;GetPointerData(-100-button,out p,true);slot.Data=p;}
            PointerEventData data=slot.Data;data.position=point;data.delta=delta;data.button=button==0?PointerEventData.InputButton.Left:PointerEventData.InputButton.Right;
            UiBridge ui=VrPlugin.Instance.UI;
            data.pointerCurrentRaycast=new RaycastResult{gameObject=target,module=ui.Raycaster,screenPosition=point,worldPosition=ui.WorldPoint(point)};
            if(button==0){
                GameObject legacy=Legacy(target);
                if(hover!=legacy){if(hover!=null)hover.SendMessage("OnMouseExit",SendMessageOptions.DontRequireReceiver);hover=legacy;if(hover!=null)hover.SendMessage("OnMouseEnter",SendMessageOptions.DontRequireReceiver);}
                HandlePointerExitAndEnter(data,target);
                if(hover!=null)hover.SendMessage("OnMouseOver",SendMessageOptions.DontRequireReceiver);
            }
            if(down){
                Release(slot,null,false);
                data.pressPosition=point;data.pointerPressRaycast=data.pointerCurrentRaycast;data.eligibleForClick=true;data.dragging=false;data.useDragThreshold=true;
                GameObject pressed=ExecuteEvents.ExecuteHierarchy(target,data,ExecuteEvents.pointerDownHandler);
                if(pressed==null)pressed=ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                slot.Pressed=pressed==null?target:pressed;slot.Legacy=Legacy(target);data.pointerPress=slot.Pressed;data.rawPointerPress=target;
                data.clickCount=slot.LastClick==slot.Pressed && Time.unscaledTime-slot.ClickTime<.3f?data.clickCount+1:1;
                data.clickTime=Time.unscaledTime;slot.ClickTime=Time.unscaledTime;slot.LastClick=slot.Pressed;
                if(button==0 && slot.Legacy!=null)slot.Legacy.SendMessage("OnMouseDown",SendMessageOptions.DontRequireReceiver);
                slot.Drag=ExecuteEvents.GetEventHandler<IDragHandler>(target);data.pointerDrag=slot.Drag;
                if(slot.Drag!=null)ExecuteEvents.Execute(slot.Drag,data,ExecuteEvents.initializePotentialDrag);
                if(slot.Pressed!=null){eventSystem.SetSelectedGameObject(slot.Pressed,data);focus=slot.Pressed.GetComponent<Button>();}
            }
            if(held && slot.Pressed!=null){
                if(button==0 && slot.Legacy!=null)slot.Legacy.SendMessage("OnMouseDrag",SendMessageOptions.DontRequireReceiver);
                if(slot.Drag!=null){
                    if(!data.dragging && (point-data.pressPosition).sqrMagnitude>=eventSystem.pixelDragThreshold*eventSystem.pixelDragThreshold){ExecuteEvents.Execute(slot.Drag,data,ExecuteEvents.beginDragHandler);data.dragging=true;data.eligibleForClick=false;}
                    if(data.dragging)ExecuteEvents.Execute(slot.Drag,data,ExecuteEvents.dragHandler);
                }
            }
            if(up)Release(slot,target,true);
        }
        static GameObject Legacy(GameObject target){if(target==null)return null;Selectable selectable=target.GetComponentInParent<Selectable>();if(selectable!=null && !selectable.IsInteractable())return null;GameObject owner=ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);if(owner==null)owner=ExecuteEvents.GetEventHandler<IPointerDownHandler>(target);return owner==null?target:owner;}
        void Release(Slot slot,GameObject target,bool click) {
            PointerEventData data=slot.Data;if(data==null)return;
            if(slot.Pressed!=null){
                ExecuteEvents.Execute(slot.Pressed,data,ExecuteEvents.pointerUpHandler);
                if(data.button==PointerEventData.InputButton.Left && slot.Legacy!=null)slot.Legacy.SendMessage("OnMouseUp",SendMessageOptions.DontRequireReceiver);
                GameObject handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                if(click && data.eligibleForClick && slot.Pressed==handler){ExecuteEvents.Execute(slot.Pressed,data,ExecuteEvents.pointerClickHandler);if(data.button==PointerEventData.InputButton.Left && slot.Legacy!=null)slot.Legacy.SendMessage("OnMouseUpAsButton",SendMessageOptions.DontRequireReceiver);}
            }
            if(data.dragging && slot.Drag!=null){if(click)ExecuteEvents.ExecuteHierarchy(target,data,ExecuteEvents.dropHandler);ExecuteEvents.Execute(slot.Drag,data,ExecuteEvents.endDragHandler);}
            slot.Pressed=slot.Drag=slot.Legacy=null;data.pointerPress=data.rawPointerPress=data.pointerDrag=null;data.eligibleForClick=data.dragging=false;
        }
        public override void DeactivateModule(){Release(primary,null,false);Release(secondary,null,false);if(primary.Data!=null)HandlePointerExitAndEnter(primary.Data,null);if(hover!=null)hover.SendMessage("OnMouseExit",SendMessageOptions.DontRequireReceiver);hover=null;focus=null;initialized=false;base.DeactivateModule();}
    }
}
