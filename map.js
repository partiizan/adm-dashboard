import {securityColor, recentReports} from './core.js';
export class StarMap {
  constructor(canvas, universe, onSelect) {
    Object.assign(this,{canvas,universe,onSelect,scale:1,ox:0,oy:0,route:[],reports:[],sov:new Map(),stale:true,showAdm:true,selected:null,points:new Map(),pointers:new Map()});
    this.ctx=canvas.getContext('2d');
    new ResizeObserver(()=>{this.resize();}).observe(canvas.parentElement);
    canvas.addEventListener('wheel',e=>{e.preventDefault();const p=this.position(e);this.zoom(Math.exp(-e.deltaY*.0015),p);},{passive:false});
    canvas.addEventListener('pointerdown',e=>{
      if(e.pointerType==='mouse' && e.button!==0)return;
      canvas.focus({preventScroll:true});canvas.setPointerCapture(e.pointerId);
      this.pointers.set(e.pointerId,this.position(e));this.down=this.position(e);this.moved=false;
      if(this.pointers.size>1)this.moved=true;
      canvas.classList.add('dragging');document.querySelector('#map-tooltip').hidden=true;
    });
    canvas.addEventListener('pointermove',e=>{
      const p=this.position(e);
      if(!this.pointers.has(e.pointerId)){this.tooltip(p);return;}
      const before=[...this.pointers.values()];const old=this.pointers.get(e.pointerId);this.pointers.set(e.pointerId,p);
      if(this.pointers.size===2){
        const after=[...this.pointers.values()];const distance=a=>Math.hypot(a[0].x-a[1].x,a[0].y-a[1].y);
        const middle=a=>({x:(a[0].x+a[1].x)/2,y:(a[0].y+a[1].y)/2});
        const b=middle(before),a=middle(after);if(distance(before)>1)this.zoom(distance(after)/distance(before),b);
        this.ox+=a.x-b.x;this.oy+=a.y-b.y;this.moved=true;this.draw();
      }else if(this.pointers.size===1){
        if(Math.hypot(p.x-this.down.x,p.y-this.down.y)>4)this.moved=true;
        if(this.moved){this.ox+=p.x-old.x;this.oy+=p.y-old.y;this.draw();}
      }
    });
    const release=(e,cancel=false)=>{
      if(!this.pointers.has(e.pointerId))return;
      if(!this.moved && !cancel){const name=this.hit(this.position(e));if(name)this.onSelect(name);}
      this.pointers.delete(e.pointerId);if(!this.pointers.size)canvas.classList.remove('dragging');
    };
    canvas.addEventListener('pointerup',e=>release(e));canvas.addEventListener('pointercancel',e=>release(e,true));
    canvas.addEventListener('pointerleave',()=>{document.querySelector('#map-tooltip').hidden=true;});
    canvas.addEventListener('keydown',e=>{
      if(['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','+','=','-','f','F'].includes(e.key))e.preventDefault();
      if(e.key==='ArrowUp')this.oy+=35;if(e.key==='ArrowDown')this.oy-=35;if(e.key==='ArrowLeft')this.ox+=35;if(e.key==='ArrowRight')this.ox-=35;
      if(e.key==='+'||e.key==='=')this.zoom(1.25);if(e.key==='-')this.zoom(.8);if(e.key.toLowerCase()==='f')this.fit();this.draw();
    });
  }
  position(e){const r=this.canvas.getBoundingClientRect();return{x:e.clientX-r.left,y:e.clientY-r.top};}
  resize(){const r=this.canvas.parentElement.getBoundingClientRect();if(r.width<1||r.height<1)return;this.width=r.width;this.height=r.height;const dpr=Math.min(devicePixelRatio||1,3);this.canvas.width=Math.round(r.width*dpr);this.canvas.height=Math.round(r.height*dpr);this.ctx.setTransform(dpr,0,0,dpr,0,0);this.fit();}
  setRegion(region){this.region=region;this.fit();}
  fit(){if(!this.region||!this.width)return;const xs=this.region.nodes.map(n=>n.x),ys=this.region.nodes.map(n=>n.y),a=Math.min(...xs),b=Math.max(...xs),c=Math.min(...ys),d=Math.max(...ys);this.scale=Math.max(.05,Math.min((this.width-90)/Math.max(1,b-a),(this.height-85)/Math.max(1,d-c)));this.ox=this.width/2-(a+b)/2*this.scale;this.oy=this.height/2-(c+d)/2*this.scale;this.draw();}
  zoom(factor,p={x:this.width/2,y:this.height/2}){const next=Math.max(.05,Math.min(15,this.scale*factor));factor=next/this.scale;this.ox=p.x-(p.x-this.ox)*factor;this.oy=p.y-(p.y-this.oy)*factor;this.scale=next;this.draw();}
  hit(p){let name=null,best=21;for(const [n,v]of this.points){const d=Math.hypot(p.x-v.x,p.y-v.y);if(d<best){best=d;name=n;}}return name;}
  tooltip(p){const tip=document.querySelector('#map-tooltip'),name=this.hit(p);if(!name){tip.hidden=true;return;}const s=this.universe.lookup(name),v=this.sov.get(s.id);tip.textContent=`${s.name} · ${s.region}\nSecurity ${s.security.toFixed(2)} · ${s.jumps.length} gates\nADM ${v?.adm!=null?v.adm.toFixed(1)+'×'+(this.stale?' (cached)':''):v&&['faction','unclaimed'].includes(v.kind)?'N/A':'not reported'}`;tip.hidden=false;tip.style.left=Math.min(p.x+15,Math.max(0,this.width-220))+'px';tip.style.top=Math.min(p.y+15,this.height-85)+'px';}
  draw(){
    const c=this.ctx,w=this.width,h=this.height;if(!w||!h)return;c.clearRect(0,0,w,h);c.fillStyle='#1c2b3b';for(let x=22;x<w;x+=32)for(let y=20;y<h;y+=32)c.fillRect(x,y,1,1);if(!this.region)return;
    this.points=new Map(this.region.nodes.map(n=>[n.name,{x:n.x*this.scale+this.ox,y:n.y*this.scale+this.oy}]));
    const edges=new Set();for(let i=1;i<this.route.length;i++){edges.add(this.route[i-1].name+'|'+this.route[i].name);edges.add(this.route[i].name+'|'+this.route[i-1].name);}
    const recent=recentReports(this.reports);
    for(const n of this.region.nodes){const sys=this.universe.lookup(n.name),start=this.points.get(n.name);for(const jump of sys.jumps){const end=this.points.get(jump);if(!end||n.name>=jump)continue;const r=edges.has(n.name+'|'+jump);c.beginPath();c.moveTo(start.x,start.y);c.lineTo(end.x,end.y);c.strokeStyle=r?'#55dfc5':'#344357';c.lineWidth=r?2.5:1;c.stroke();}}
    const circle=(p,r,color,width=1)=>{c.beginPath();c.arc(p.x,p.y,r,0,Math.PI*2);c.strokeStyle=color;c.lineWidth=width;c.stroke();};
    c.textAlign='center';c.textBaseline='top';const labels=[];
    for(const n of [...this.region.nodes].sort((a,b)=>Number(b.name===this.selected)-Number(a.name===this.selected))){const p=this.points.get(n.name);if(p.x < -70||p.y < -35||p.x > w+70||p.y > h+35)continue;const sys=this.universe.lookup(n.name),report=recent.get(n.name),sov=this.sov.get(sys.id);
      if(report){circle(p,13,report.clear?'#6dddaf':'#ff997e',1.8);circle(p,18,report.clear?'#426856':'#735043',.7);}
      if(n.name===this.selected)circle(p,9,'#edf6ff',1.6);
      c.fillStyle='#0a111b';c.beginPath();c.arc(p.x,p.y,4.2,0,Math.PI*2);c.fill();circle(p,4.2,securityColor(sys.security),n.outside?1:1.7);
      if(!n.outside){c.fillStyle=securityColor(sys.security);c.beginPath();c.arc(p.x,p.y,1.5,0,Math.PI*2);c.fill();}
      const box={x:p.x-n.name.length*3-3,y:p.y+7,w:n.name.length*6+6,h:this.showAdm?25:13};if(labels.some(b=>box.x<b.x+b.w&&box.x+box.w>b.x&&box.y<b.y+b.h&&box.y+box.h>b.y))continue;labels.push(box);
      c.font='10px -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif';c.fillStyle=n.outside?'#8092a9':'#dce6f2';c.fillText(n.name,p.x,p.y+8);
      if(this.showAdm){const text=sov?.adm!=null?sov.adm.toFixed(1)+'×'+(this.stale?'*':''):sov&&['faction','unclaimed'].includes(sov.kind)?'N/A':'—';c.font='9px -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif';c.fillStyle=this.stale?'#d9b573':'#55dfc5';c.fillText(text,p.x,p.y+20);}
    }
  }
}
