const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["_astro/ui-core.D_tD7AES.js","_astro/preload-helper.BlTxHScW.js"])))=>i.map(i=>d[i]);
import{_ as l}from"./preload-helper.BlTxHScW.js";document.addEventListener("astro:page-load",d);function d(){if(!document.querySelector("#pagefind-search"))return;const t=new URLSearchParams(window.location.search);(window.requestIdleCallback||(n=>setTimeout(n,1)))(async()=>{try{let r=function(e){e.target.value===""&&history.replaceState(history.state,"",window.location.pathname)};var n=r;const{PagefindUI:a}=await l(async()=>{const{PagefindUI:e}=await import("./ui-core.D_tD7AES.js");return{PagefindUI:e}},__vite__mapDeps([0,1])),i=new a({element:"#pagefind-search",showImages:!1,processTerm:function(e){return t.set("q",e),history.replaceState(history.state,"","?"+t.toString()),e}}),c=t.get("q");c&&i.triggerSearch(c);const o=document.querySelector(".pagefind-ui__search-input"),s=document.querySelector(".pagefind-ui__search-clear");o?.addEventListener("input",r),s?.addEventListener("click",r)}catch(r){console.error("Error initializing search:",r);const a=document.querySelector("#search-container");a&&(a.innerHTML=`
            <div class="alert alert-error shadow-lg">
              <div>
                <Icon name="lucide:alert-triangle" class="w-6 h-6" />
                <span>Error loading search functionality. Please try again later.</span>
              </div>
            </div>
          `)}})}
