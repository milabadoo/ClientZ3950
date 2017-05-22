#region License and Copyright

//          SobekCM MARC Library ( Version 1.2 )
//          
//          Copyright (2005-2012) Mark Sullivan. ( Mark.V.Sullivan@gmail.com )
//          
//          This file is part of SobekCM MARC Library.
//          
//          SobekCM MARC Library is free software: you can redistribute it and/or modify
//          it under the terms of the GNU Lesser Public License as published by
//          the Free Software Foundation, either version 3 of the License, or
//          (at your option) any later version.
//            
//          SobekCM MARC Library is distributed in the hope that it will be useful,
//          but WITHOUT ANY WARRANTY; without even the implied warranty of
//          MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//          GNU Lesser Public License for more details.
//            
//          You should have received a copy of the GNU Lesser Public License
//          along with SobekCM MARC Library.  If not, see <http://www.gnu.org/licenses/>.

#endregion

#region Using directives

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml;
using SobekCM.Bib_Package.MARC.Parsers;
using SobekCMMarcLibrary;
using SobekCMMarcLibrary.ErrorHandling;
using SobekCM_Marc_Library;
using SobekCM_Marc_Library.Parsers;
using SobekCM_Marc_Library.Writers;

#endregion

namespace SobekCM.Bib_Package.MARC
{
    /// <summary> Stores all the information from a MARC21 record </summary>
    /// <remarks>Object created by Mark V Sullivan (2006) for University of Florida's Digital Library Center.</remarks>
    public class MarcRecord
    {
        private string _controlNumber;
        private readonly SortedList<int, List<MarcField>> _fields;
        private string _leader;
        private List<MarcRecordParsingWarning> _warnings;
        private List<MarcRecordParsingError> _errors;

        /// <summary> Constructor for a new instance of the MARC_XML_Record class </summary>
        public MarcRecord()
        {
            _leader = String.Empty;
            _fields = new SortedList<int, List<MarcField>>();
            ErrorFlag = false;
        }

        #region Public properties

        /// <summary> Control number for this record from the 001 field </summary>
        /// <remarks> This is used when importing directly from MARC records into the SobekCM library </remarks>
        public string ControlNumber
        {
            get
            {
                if (_controlNumber != null)
                    return _controlNumber;

                _controlNumber = _fields.ContainsKey(1) ? _fields[1][0].ControlFieldValue : String.Empty;

                return _controlNumber;
            }
        }

        /// <summary> Flag is set if there is an error detected while reading this MARC
        /// record from a MARC21 Exchange Format file </summary>
        /// <remarks> This is used when importing directly from MARC records into the SobekCM library </remarks>
        public bool ErrorFlag { get; set; }

        /// <summary> Gets or sets the leader portion of this MARC21 Record </summary>
        public string Leader
        {
            get
            {
                // First, compute the overall length of this record
                var totalLength = 0;
                var directoryLength = 25;
                var allTags = SortedMarcTagList;
                foreach (MarcField thisTag in allTags)
                {
                    totalLength = totalLength + 5 + thisTag.ControlFieldValue.Length;
                    directoryLength += 12;
                }

                string totalLengthString = (totalLength.ToString()).PadLeft(5, '0');
                string totalDirectoryString = (directoryLength.ToString()).PadLeft(5, '0');

                if (_leader.Length == 0)
                {
                    return totalLengthString + "nam  22" + totalDirectoryString + "3a 4500";
                }
                return totalLengthString + _leader.Substring(5, 7) + totalDirectoryString + _leader.Substring(17);
            }
            set { _leader = value; }
        }

        /// <summary> Gets the collection of MARC fields, by MARC tag number </summary>
        /// <param name="Tag"> MARC tag number to return all matching fields </param>
        /// <returns> Collection of matching tags, or an empty read only collection </returns>
        public ReadOnlyCollection<MarcField> this[int Tag]
        {
            get
            {
                if (_fields.ContainsKey(Tag))
                    return new ReadOnlyCollection<MarcField>(_fields[Tag]);
                return new ReadOnlyCollection<MarcField>(new List<MarcField>());
            }
        }

        /// <summary> Returns a list of all the MARC tags, sorted by tag number and
        /// ready to display as a complete MARC record </summary>
        public List<MarcField> SortedMarcTagList
        {
            get
            {
                List<MarcField> returnValue = new List<MarcField>();

                foreach (List<MarcField> fieldsByTag in _fields.Values)
                {
                    returnValue.AddRange(fieldsByTag);
                }

                return returnValue;
            }
        }

        #endregion

        #region Public methods to check if a field exists, add a field, etc...

        /// <summary> Gets a flag indicating if a particular field exists </summary>
        /// <param name="Tag">Tag for the MARC field to check</param>
        /// <returns>TRUE if the field exists, otherwise FALSE</returns>
        public bool has_Field(int Tag)
        {
            return _fields.ContainsKey(Tag);
        }

        /// <summary> Add a new control field to this record </summary>
        /// <param name="tag">Tag for new control field</param>
        /// <param name="controlFieldValue">Data for the new control field</param>
        /// <returns>New control field object created and added</returns>
        public MarcField Add_Field(int tag, string controlFieldValue)
        {
            // Create the new control field
            MarcField newField = new MarcField(tag, controlFieldValue);

            // Either add this to the existing list, or create a new one
            if (_fields.ContainsKey(tag))
                _fields[tag].Add(newField);
            else
            {
                List<MarcField> newTagCollection = new List<MarcField> {newField};
                _fields[tag] = newTagCollection;
            }

            // Return the newlly built control field
            return newField;
        }

        /// <summary> Add a new data field to this record </summary>
        /// <param name="tag">Tag for new data field</param>
        /// <param name="indicator1">First indicator for new data field</param>
        /// <param name="indicator2">Second indicator for new data field</param>
        /// <returns>New data field object created and added</returns>
        public MarcField Add_Field(int tag, char indicator1, char indicator2)
        {
            // Create the new datafield
            var newField = new MarcField(tag, indicator1, indicator2);

            // Either add this to the existing list, or create a new one
            if (_fields.ContainsKey(tag))
                _fields[tag].Add(newField);
            else
            {
                var newTagCollection = new List<MarcField> {newField};
                _fields[tag] = newTagCollection;
            }

            // Return the newlly built data field
            return newField;
        }

        /// <summary> Add a new data field to this record </summary>
        /// <param name="tag">Tag for new data field</param>
        /// <param name="indicators">Both indicators</param>
        /// <param name="controlFieldValue">Value for this control field </param>
        /// <returns>New data field object created and added</returns>
        public MarcField Add_Field(int tag, string indicators, string controlFieldValue)
        {
            // Create the new datafield
            var newField = new MarcField(tag, controlFieldValue) {Indicators = indicators};

            // Either add this to the existing list, or create a new one
            if (_fields.ContainsKey(tag))
                _fields[tag].Add(newField);
            else
            {
                var newTagCollection = new List<MarcField> {newField};
                _fields[tag] = newTagCollection;
            }

            // Return the newlly built data field
            return newField;
        }

        /// <summary> Adds a new field to this record </summary>
        /// <param name="newField"> New field to add </param>
        public void Add_Field(MarcField newField)
        {
            if (newField == null)
                return;

            // Either add this to the existing list, or create a new one
            if (_fields.ContainsKey(newField.Tag))
                _fields[newField.Tag].Add(newField);
            else
            {
                var newTagCollection = new List<MarcField> {newField};
                _fields[newField.Tag] = newTagCollection;
            }
        }

        /// <summary> Gets data from a particular subfield within a singular data field  </summary>
        /// <param name="tag">Tag for new data field</param>
        /// <param name="subfield">Code for the subfield in question</param>
        /// <returns>The value of the subfield, or an empty string </returns>
        /// <remarks> If there are multiple instances of this subfield, then they are returned 
        /// together with a '|' delimiter between them </remarks>
        public string Get_Data_Subfield(int tag, char subfield)
        {
            if ((_fields.ContainsKey(tag)) && (_fields[tag][0].has_Subfield(subfield)))
                return _fields[tag][0][subfield];
            return string.Empty;
        }

        /// <summary> Removes all occurrences of a tag </summary>
        /// <param name="tagNumber"> Tag number of the MARC tags to remove </param>
        public void Remove_Tag(int tagNumber)
        {
            // Remove from the list of fields
            if (_fields.ContainsKey(tagNumber))
                _fields.Remove(tagNumber);
        }

        #endregion

        #region Methods to read from a MarcXML file directly into this structure

        /// <summary> Reads the data from a MARC XML file into this record </summary>
        /// <param name="marcXmlFile">Input MARC XML file</param>
        /// <returns>TRUE if successful, otherwise FALSE </returns>
        public bool Read_From_MARC_XML_File(string marcXmlFile)
        {
            return MarcxmlParser.Read_From_MARC_XML_File(marcXmlFile, this);
        }

        /// <summary> Reads the data from a XML Node Reader </summary>
        /// <param name="nodeReader">XML Node Reader </param>
        /// <returns>TRUE if successful, otherwise FALSE </returns>
        public bool Read_MARC_Info(XmlTextReader nodeReader)
        {
            return MarcxmlParser.Read_MARC_Info(nodeReader, this);
        }

        #endregion

        #region Methods to get or save the MarcXML for this record 

        /// <summary> Saves this MARC records as MARC XML </summary>
        /// <param name="filename"> Filename to save this MARC record as </param>
        /// <returns> TRUE if successful, otherwise FALSE </returns>
        public bool Save_MARC_XML(string filename)
        {
            try
            {
                var str = To_MARC_XML();

                var fromEncodind = Encoding.GetEncoding(1252);//�� ����� ���������
                var bytes = fromEncodind.GetBytes(str);
                var toEncoding = Encoding.GetEncoding("koi8-r");//� ����� ���������
                str = toEncoding.GetString(bytes);


                var writer = new StreamWriter(filename, false);
                writer.Write(str);
                writer.Flush();
                writer.Close();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary> Returns this MARC record as MARC XML </summary>
        /// <returns> This record as MARC XML </returns>
        public string To_MARC_XML()
        {
            return MarcxmlWriter.To_MarcXML(this);
        }

        #endregion

        #region Methods to get or save the MARC21 for this record

        /// <summary> Saves this MARC record as MARC21 Exchange format record data file </summary>
        /// <param name="filename"> Filename to save this MARC record as  </param>
        /// <returns> TRUE if successful, otherwise FALSE </returns>
        public bool Save_MARC21(string filename)
        {
            try
            {
                // This code below was added to prevent the resulting
                // MARC21 file from having the UTF-8 Byte-Order Marks encoding bytes
                // ( 0xEF,0xBB,0xBF ) included in the MARC21 file
                MemoryStream ms = new MemoryStream();

                StreamWriter writer = new StreamWriter(ms, Encoding.UTF8);
                writer.Write(To_Machine_Readable_Record());
                writer.Flush();

                ms.Seek(0, SeekOrigin.Begin);

                FileStream fs = File.Create(filename);
                fs.Write(ms.GetBuffer(), 3, (int) (ms.Length - 3));
                fs.Flush();
                fs.Close();
                writer.Close();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Returns a string which represents this record in machine readable record format. </summary>
        /// <returns> This MARC record as MARC21 Exchange format record string</returns>
        public string To_Machine_Readable_Record()
        {
            return Marc21ExchangeFormatWriter.To_Machine_Readable_Record(this);
        }

        #endregion

        #region Method overrides the ToString() method 

        /// <summary> Outputs this record as a string </summary>
        /// <returns></returns>
        public override string ToString()
        {
            // Create the StringBuilder
            StringBuilder returnVal = new StringBuilder(2000);

            // Add the leader
            returnVal.Append("LDR " + Leader + "\r\n");

            // Step through each field in the collection
            foreach (int thisTag in _fields.Keys)
            {
                List<MarcField> matchingFields = _fields[thisTag];
                foreach (MarcField thisField in matchingFields)
                {
                    if (thisField.SubfieldCount == 0)
                    {
                        if (thisField.ControlFieldValue.Length > 0)
                        {
                            returnVal.Append(thisField.Tag.ToString().PadLeft(3, '0') + " " +
                                             thisField.ControlFieldValue + "\r\n");
                        }
                    }
                    else
                    {
                        returnVal.Append(thisField.Tag.ToString().PadLeft(3, '0') + " " + thisField.Indicators);

                        // Build the complete line
                        foreach (MarcSubfield thisSubfield in thisField.Subfields)
                        {
                            if (thisSubfield.SubfieldCode == ' ')
                            {
                                returnVal.Append(" " + thisSubfield.Data);
                            }
                            else
                            {
                                returnVal.Append(" |" + thisSubfield.SubfieldCode + " " + thisSubfield.Data);
                            }
                        }

                        returnVal.Append("\r\n");
                    }
                }
            }

            // Return the built string
            return returnVal.ToString();
        }

        #endregion

        #region Methods to handle the list of warnings stored in this MARC record object

        /// <summary> Add a new warning which occurred during parsing to this MARC record object </summary>
        /// <param name="Warning"> Warning object to add to the list </param>
        public void Add_Warning(MarcRecordParsingWarning Warning)
        {
            // Ensure the list is built
            if (_warnings == null)
                _warnings = new List<MarcRecordParsingWarning>();

            // If no other warning of the same type exists, add this
            if (!_warnings.Contains(Warning))
                _warnings.Add(Warning);
        }

        /// <summary> Add a new warning which occurred during parsing to this MARC record object </summary>
        /// <param name="Warning_Type"> Type of this warning </param>
        /// <param name="Warning_Details"> Any additional information about a warning </param>
        public void Add_Warning(MarcRecordParsingWarningTypeEnum Warning_Type, string Warning_Details)
        {
            // Ensure the list is built
            if (_warnings == null)
                _warnings = new List<MarcRecordParsingWarning>();

            // Build this warning object
            MarcRecordParsingWarning Warning = new MarcRecordParsingWarning(Warning_Type, Warning_Details);

            // If no other warning of the same type exists, add this
            if (!_warnings.Contains(Warning))
                _warnings.Add(Warning);
        }

        /// <summary> Add a new warning which occurred during parsing to this MARC record object </summary>
        /// <param name="Warning_Type"> Type of this warning </param>
        public void Add_Warning(MarcRecordParsingWarningTypeEnum Warning_Type)
        {
            // Ensure the list is built
            if (_warnings == null)
                _warnings = new List<MarcRecordParsingWarning>();

            // Build this warning object
            MarcRecordParsingWarning Warning = new MarcRecordParsingWarning(Warning_Type);

            // If no other warning of the same type exists, add this
            if (!_warnings.Contains(Warning))
                _warnings.Add(Warning);
        }

        /// <summary> Returns a flag indicating if there are any warnings associated with this record  </summary>
        public bool Has_Warnings
        {
            get { return (_warnings != null) && (_warnings.Count != 0); }
        }

        /// <summary> Returns the list of warnings associated with this MARC record  </summary>
        public ReadOnlyCollection<MarcRecordParsingWarning> Warnings
        {
            get { return _warnings == null ? null : new ReadOnlyCollection<MarcRecordParsingWarning>(_warnings); }
        }

        #endregion

        #region Methods to handle the list of errors stored in this MARC record object

        /// <summary> Add a new error which occurred during parsing to this MARC record object </summary>
        /// <param name="Error"> Error object to add to the list </param>
        public void Add_Error(MarcRecordParsingError Error)
        {
            // Ensure the list is built
            if (_errors == null)
                _errors = new List<MarcRecordParsingError>();

            // If no other error of the same type exists, add this
            if (!_errors.Contains(Error))
                _errors.Add(Error);
        }

        /// <summary> Add a new error which occurred during parsing to this MARC record object </summary>
        /// <param name="Error_Type"> Type of this error </param>
        /// <param name="Error_Details"> Any additional information about an error </param>
        public void Add_Error(MarcRecordParsingErrorTypeEnum Error_Type, string Error_Details)
        {
            // Ensure the list is built
            if (_errors == null)
                _errors = new List<MarcRecordParsingError>();

            // Build this Error object
            MarcRecordParsingError Error = new MarcRecordParsingError(Error_Type, Error_Details);

            // If no other Error of the same type exists, add this
            if (!_errors.Contains(Error))
                _errors.Add(Error);
        }

        /// <summary> Add a new error which occurred during parsing to this MARC record object </summary>
        /// <param name="Error_Type"> Type of this error </param>
        public void Add_Error(MarcRecordParsingErrorTypeEnum Error_Type)
        {
            // Ensure the list is built
            if (_errors == null)
                _errors = new List<MarcRecordParsingError>();

            // Build this error object
            MarcRecordParsingError Error = new MarcRecordParsingError(Error_Type);

            // If no other error of the same type exists, add this
            if (!_errors.Contains(Error))
                _errors.Add(Error);
        }

        /// <summary> Returns a flag indicating if there are any errors associated with this record  </summary>
        public bool Has_Errors
        {
            get { return (_errors != null) && (_errors.Count != 0); }
        }

        /// <summary> Returns the list of erors associated with this MARC record  </summary>
        public ReadOnlyCollection<MarcRecordParsingError> Errors
        {
            get { return _errors == null ? null : new ReadOnlyCollection<MarcRecordParsingError>(_errors); }
        }

        #endregion
    }
}