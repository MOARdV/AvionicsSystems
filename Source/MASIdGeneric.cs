/*****************************************************************************
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 MOARdV
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

namespace AvionicsSystems
{
    /// <summary>
    /// MASIdPart is a generic base class for part-specific identification, allowing the
    /// player to mark specific parts with identifiers that can be used to query or
    /// control specific parts instead of adjusting parts wholesale.  The obvious
    /// initial use case for this is for engines - particularly, adjusting gimbals,
    /// enabling, etc of rocket engines, as well as some of the modded engine
    /// plugins, such as Firespitter and Advanced Jet Engine.
    /// 
    /// Some intentional limitations on the design:
    /// * Parts start with an ID of 0.
    /// * Parts with an ID of 0 are ignored for purposes of part-specific control.
    /// * If multiple parts share an active ID, only one of them is controlled.
    /// 
    /// The first two limitations mean that a part must opt-in to the control system.
    /// The last limitation avoids weird behavior that might occur if multiple parts
    /// use the same id (What would be the RPM of 3 engines?).
    /// </summary>
    public class MASIdGeneric : PartModule
    {
        /// <summary>
        /// Number of valid IDs.  Actual usable number is one less, since 0 is "ignore me".
        /// </summary>
        private readonly int maxIds = 32;

        /// <summary>
        /// ID of the part.  0 opts-out of part-specific interaction.
        /// </summary>
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#MAS_GenericId_PartId")]
        public int partId = 0;

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "#MAS_GenericId_Next_PartId")]
        public void IdPlus()
        {
            partId = (partId + 1) % maxIds;
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "#MAS_GenericId_Prev_PartId")]
        public void IdMinus()
        {
            partId--;
            if (partId < 0)
            {
                partId = maxIds - 1;
            }
        }
    }
}
