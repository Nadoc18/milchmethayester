Hi. Please, import relevant package, based on the SRP, you are using.

Thanks for your purchase. I hope you will find the asset useful.
If you have any problem, please email me: izzynab.publisher@gmail.com


--------------------------------------------------------
Built-in
--------------------------------------------------------

You can use outline shader in few different ways:
1) Add OnlyOutline shader as second material to your object
2) Add OutlineDrawer script to your object with skinned mesh renderer of mesh filter with mesh filter. Use OnlyOutline shader in Material field.
3) Use custom standard materials with outlines: OutlineMetallic and OutlineSpecular. This shaders work same as standard shaders but they do not support every feature that standard materials do.
You can easily change your old materials to new ones, all properties should remain unchanged.

Masked Outlines:
Note: Masked outlines use stencil buffer.




In order to use this feature, you need to replace your old materials with new ones: OutlineMaskMetallic or OutlineMaskSpecular.This shaders work same as standard shaders but they do not support every feature that standard materials do.
After that you need to add OnlyOutlineWithMask shader to your object either by using OutlineDrawer script or as a second material.






--------------------------------------------------------
URP
--------------------------------------------------------

You can use outline shader in few different ways:
1) Add OnlyOutline shader as second material to your object.
2) Add Renderer Feature to forward renderer with OnlyOutline override material and layer mask set to your desired layer. You can easily create new layer for each of your objects that need different outlines.
3) Use custom standard materials with outlines: OutlineMetallic and OutlineSpecular. This shaders work same as standard shaders. You can easily change your old materials to new ones, all properties will remain unchanged.


Masked Outlines:
Note: Masked outlines use stencil buffer.

In order to use this feature, you need to add two Renderer Features: one with OnlyOutlineWithMask material and second with OutlineMask material. Both of these renderer features have to draw over the same layer.
You could also just add OnlyOutlineWithMask material as a second materil to your object, but it is cleaner to make it work with Renderer Feature.
You need to create OutlineMask and OnlyOutlineWithMask materials for each object that you need to have the masked outline effect.
If you want to create few different objects each with masked outline, you need to set different value for StencilReference from 1 to 255 for each object. 

You can find examples of all of this in EasyOutlines/URP/EasyOutlineRenderer.asset

--------------------------------------------------------
HDRP
--------------------------------------------------------

Instead of using OnlyOutline shader you can modify and use shader graph version of the shader. 

You can use outline shader in few different ways:
1) Add OnlyOutline shader or Shader graph version as second material to your object.
2) Add Custom Pass: DrawRendereCustomPass with OnlyOutline override material and filter set to your desired layer. You can easily create new layer for each of your objects that need different outlines.

Masked Outlines:
Note: Masked outlines use stencil buffer.

In order to use this feature, you need to add two DrawRendereCustomPasses: one with OnlyOutlineWithMask material and second with OutlineMask material. Both of these custom passes have to draw over the same layer.
You could also just add OnlyOutlineWithMask material as a second materil to your object, but it is cleaner to make it work with DrawRendereCustomPass.
You need to create OutlineMask and OnlyOutlineWithMask materials for each object that you need to have the masked outline effect.
If you want to create few different objects each with masked outline, you need to set different value for StencilReference from 1 to 255 for each object.  

You can find examples of all of this in example scene in Custom Pass for outlines object.


--------------------------------------------------------
Properties:
--------------------------------------------------------

You can disable outline from C# by setting _Enable property to 0, and enable it by setting the property to 1.

Use Adaptive thickness to make outline thickness more steady over distance from mesh. 

There are 3 outline types: 
Normal - based on mesh normals
If your outlines do not look as good as you would like, and you don't want to bake new normals onto your meshes, try calculating normals with smoothing angle set to 180 in Import Settings of your models.
Position - based on mesh vertex positions
UV Baked - reads values baked in UV3 channel, if you need to bake to another channel,in BakeMeshNormalsToUV tool choose UV2 or UV 1
and change UV node in shader graph to read from appropriate uv channel.

Gradient:
Intesity - Scale factor of the color.
Noise Scale - Scale of the gradient noise used to map ramp onto outline. 
Screen Space - Check if you want noise to be drawed in screen space, otherwise it will use tangent space.
Flow Speed - Speed of the noise offset change.
Flow Rotation - Rotation if the noise offset change.

Ramp Map:
Create gradients by clicking on the gradient editor. When you are ready to save the texture, set path of the ramp png.
Then export the texture. After that you need to manually drag and drop the saved texture onto RampMap field on the left.

!!!If you do not export gradient as texture, it wont't be saved!!!

Tip: use presets to save your gradients.

--------------------------------------------------------
Tools:
--------------------------------------------------------

To bake mesh smoothed normals to UV channel, use Tools/BakeMeshNormalsToUV window. Please, make sure you selected UV3 channel for baking normals.
If you are not able to use UV3 channel, you have to change shader code by yourself to make it work with another uv channel. 
To do that, simply find this lines of code in the shader:

#elif defined(_OUTLINETYPE_UVBAKED)
				float3 staticSwitch14 = float3( v.texcoord3.xy ,  0.0 );

And change texcoord3 to the UV you choosed. Example:
If you baked normals to UV2 channel, change texcoord3 to texcoord2.

