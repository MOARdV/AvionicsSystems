/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2016 MOARdV
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 * 
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace AvionicsSystems
{
    public delegate object DynamicMethod<T>(T param0);
    public delegate object DynamicMethod<T, U>(T param0, U param1);
    public delegate object DynamicMethod<T, U, V, W>(T param0, U param1, V param2, W param3);
    public delegate bool DynamicMethodBool<T>(T param0);
    public delegate double DynamicMethodDouble<T>(T param0);

    // Specializations for MechJeb
    public delegate Vector3d DynamicMethodVec3d<T, U>(T param0, U param1);
    public delegate Vector3d DynamicMethodVec3d<T, U, V>(T param0, U param1, V param2);

    /// <summary>
    /// The DynamicMethodFactory provides a way to generate delegates where one
    /// or more parameters are typed in other assemblies.
    /// 
    /// The code is based on source found at
    /// http://www.codeproject.com/Articles/10951/Fast-late-bound-invocation-through-DynamicMethod-d
    /// which is covered by The Code Project Open License http://www.codeproject.com/info/cpol10.aspx
    /// </summary>
    static internal class DynamicMethodFactory
    {
        /// <summary>
        /// Create a delegate who takes a single typed parameter and returns an
        /// object (which may be null if the method returns void).
        /// </summary>
        /// <typeparam name="T">Type of the first/only parameter.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns></returns>
        static internal DynamicMethod<T> CreateFunc<T>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateFunc<T> called with static method that takes " + parms.Length + " parameters");
                }

                if (typeof(T) != parms[0].ParameterType)
                {
                    // What to do?
                }
            }
            else
            {
                if (parms.Length != 0)
                {
                    throw new ArgumentException("CreateFunc<T> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }
            //if (parms.Length != 1)
            //{
            //    throw new ArgumentException("CreateFunc<T> called with method that takes " + parms.Length + " parameters");
            //}
            //if(typeof(T) != parms[0].ParameterType)
            //{
            //    // What to do?
            //}
            //if (methodInfo.ReturnType == typeof(void))
            //{
            //    throw new ArgumentException("CreateFunc<T> called with method that returns void");
            //}

            Type[] _argTypes = { typeof(T) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(object), // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed
                if (methodInfo.ReturnType.IsValueType)
                {
                    il.Emit(OpCodes.Box, methodInfo.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (DynamicMethod<T>)dynam.CreateDelegate(typeof(DynamicMethod<T>));
        }

        /// <summary>
        /// Create a function that takes two typed parameters and returns an object
        /// (which may be null if the method returns void).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        static internal DynamicMethod<T, U> CreateFunc<T, U>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 2)
                {
                    throw new ArgumentException("CreateFunc<T, U> called with static method that takes " + parms.Length + " parameters");
                }

                if (typeof(T) != parms[0].ParameterType)
                {
                    // What to do?
                }
                if (typeof(U) != parms[1].ParameterType)
                {
                    // What to do?
                }
            }
            else
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateFunc<T, U> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                if (typeof(U) != parms[0].ParameterType)
                {
                    // What to do?
                }
            }

            //if (methodInfo.ReturnType == typeof(void))
            //{
            //    throw new ArgumentException("CreateFunc<T> called with method that returns void");
            //}

            Type[] _argTypes = { typeof(T), typeof(U) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(object), // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed
                if (methodInfo.ReturnType.IsValueType)
                {
                    il.Emit(OpCodes.Box, methodInfo.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (DynamicMethod<T, U>)dynam.CreateDelegate(typeof(DynamicMethod<T, U>));
        }

        static internal DynamicMethod<T, U, V, W> CreateFunc<T, U, V, W>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 4)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> called with static method that takes " + parms.Length + " parameters");
                }

                if (typeof(T) != parms[0].ParameterType)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> parameter [0] mismatch");
                }
                if (typeof(U) != parms[1].ParameterType)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> parameter [1] mismatch");
                }
                if (typeof(V) != parms[2].ParameterType)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> parameter [2] mismatch");
                }
                if (typeof(W) != parms[3].ParameterType)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> parameter [3] mismatch");
                }
            }
            else
            {
                if (parms.Length != 3)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                if (typeof(U) != parms[0].ParameterType)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> parameter [0] mismatch");
                }
                if (typeof(V) != parms[1].ParameterType)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> parameter [1] mismatch");
                }
                if (typeof(W) != parms[2].ParameterType)
                {
                    throw new ArgumentException("CreateFunc<T, U, V, W> parameter [2] mismatch");
                }
            }

            Type[] _argTypes = { typeof(T), typeof(U), typeof(V), typeof(W) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                typeof(object), // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                // If result is of value type it needs to be boxed
                if (methodInfo.ReturnType.IsValueType)
                {
                    il.Emit(OpCodes.Box, methodInfo.ReturnType);
                }
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (DynamicMethod<T, U, V, W>)dynam.CreateDelegate(typeof(DynamicMethod<T, U, V, W>));
        }

        /// <summary>
        /// Create a delegate who takes a single typed parameter and returns a
        /// boolean.
        /// </summary>
        /// <typeparam name="T">Type of the first/only parameter.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns></returns>
        static internal DynamicMethodBool<T> CreateFuncBool<T>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateFunc<T> called with static method that takes " + parms.Length + " parameters");
                }

                if (typeof(T) != parms[0].ParameterType)
                {
                    // What to do?
                }
            }
            else
            {
                if (parms.Length != 0)
                {
                    throw new ArgumentException("CreateFunc<T, U> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }
            if (methodInfo.ReturnType != typeof(bool))
            {
                throw new ArgumentException("CreateFunc<T> called with method that returns void");
            }

            Type[] _argTypes = { typeof(T) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                methodInfo.ReturnType, // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (DynamicMethodBool<T>)dynam.CreateDelegate(typeof(DynamicMethodBool<T>));
        }

        /// <summary>
        /// Create a delegate who takes a single typed parameter and returns a
        /// double.
        /// </summary>
        /// <typeparam name="T">Type of the first/only parameter.</typeparam>
        /// <param name="methodInfo">MethodInfo describing the method we're calling.</param>
        /// <returns></returns>
        static internal DynamicMethodDouble<T> CreateFuncDouble<T>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateFunc<T> called with static method that takes " + parms.Length + " parameters");
                }

                if (typeof(T) != parms[0].ParameterType)
                {
                    // What to do?
                }
            }
            else
            {
                if (parms.Length != 0)
                {
                    throw new ArgumentException("CreateFunc<T, U> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
            }
            if (methodInfo.ReturnType != typeof(double))
            {
                throw new ArgumentException("CreateFunc<T> called with method that returns void");
            }

            Type[] _argTypes = { typeof(T) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                methodInfo.ReturnType, // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (DynamicMethodDouble<T>)dynam.CreateDelegate(typeof(DynamicMethodDouble<T>));
        }

        static internal DynamicMethodVec3d<T, U> CreateFuncVec3d<T, U>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 2)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> called with static method that takes " + parms.Length + " parameters");
                }

                if (typeof(T) != parms[0].ParameterType)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> parameter [0] mismatch");
                }
                if (typeof(U) != parms[1].ParameterType)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> parameter [1] mismatch");
                }
            }
            else
            {
                if (parms.Length != 1)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                if (typeof(U) != parms[0].ParameterType)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> parameter [0] mismatch");
                }
            }

            if (methodInfo.ReturnType != typeof(Vector3d))
            {
                throw new ArgumentException("CreateFuncVec3d<T, U, V> called with method that does not return Vector3d");
            }

            Type[] _argTypes = { typeof(T), typeof(U) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                methodInfo.ReturnType, // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (DynamicMethodVec3d<T, U>)dynam.CreateDelegate(typeof(DynamicMethodVec3d<T, U>));
        }
        
        static internal DynamicMethodVec3d<T, U, V> CreateFuncVec3d<T, U, V>(MethodInfo methodInfo)
        {
            // Up front validation:
            ParameterInfo[] parms = methodInfo.GetParameters();
            if (methodInfo.IsStatic)
            {
                if (parms.Length != 3)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> called with static method that takes " + parms.Length + " parameters");
                }

                if (typeof(T) != parms[0].ParameterType)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> parameter [0] mismatch");
                }
                if (typeof(U) != parms[1].ParameterType)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> parameter [1] mismatch");
                }
                if (typeof(V) != parms[2].ParameterType)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> parameter [2] mismatch");
                }
            }
            else
            {
                if (parms.Length != 2)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> called with non-static method that takes " + parms.Length + " parameters");
                }
                // How do I validate T?
                //if (typeof(T) != parms[0].ParameterType)
                //{
                //    // What to do?
                //}
                if (typeof(U) != parms[0].ParameterType)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> parameter [0] mismatch");
                }
                if (typeof(V) != parms[1].ParameterType)
                {
                    throw new ArgumentException("CreateFuncVec3d<T, U, V> parameter [1] mismatch");
                }
            }

            if (methodInfo.ReturnType != typeof(Vector3d))
            {
                throw new ArgumentException("CreateFuncVec3d<T, U, V> called with method that does not return Vector3d");
            }

            Type[] _argTypes = { typeof(T), typeof(U), typeof(V) };

            // Create dynamic method and obtain its IL generator to
            // inject code.
            DynamicMethod dynam =
                new DynamicMethod(
                "", // name - don't care
                methodInfo.ReturnType, // return type
                _argTypes, // argument types
                typeof(DynamicMethodFactory));
            ILGenerator il = dynam.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);

            // Perform actual call.
            // If method is not final a callvirt is required
            // otherwise a normal call will be emitted.
            if (methodInfo.IsFinal)
            {
                il.Emit(OpCodes.Call, methodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, methodInfo);
            }

            // Emit return opcode.
            il.Emit(OpCodes.Ret);


            return (DynamicMethodVec3d<T, U, V>)dynam.CreateDelegate(typeof(DynamicMethodVec3d<T, U, V>));
        }
    }
}
