import { EnglishOnlyCharacters } from "./englistOnlyCharacters";

export interface PaparseConfig {
    delimiter : string;
    hasHeader : boolean;
    skipEmptyLines : boolean;
    quoteCharacter : string;
    worker : boolean;
    useOnlyRomanNumerals : boolean;
    useOnlyEnglishLetters : boolean;
    englishOnlyCharacters : EnglishOnlyCharacters[];
}